using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services;

public sealed class DependencyInjectionTests
{
    private static readonly Type[] Transients = [typeof(MergeEngine), typeof(TemplateValidator), typeof(DocxStoryReader), typeof(TemplateStructureValidator), typeof(DocumentRenderer), typeof(DataResolver), typeof(OoxmlFieldParser), typeof(FieldCodeParser), typeof(RegionParser), typeof(ValueFormatter), typeof(CachedResultWriter)];
    private static readonly Type[] Singletons = [typeof(FormatterRegistry), typeof(RubSpellOutFormatter)];

    [Fact]
    public void Registration_returns_collection_and_defines_complete_exact_default_graph()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddMonoReport());
        Assert.Equal(Transients.Length + Singletons.Length, services.Count);
        foreach (var type in Transients) Assert.Equal(ServiceLifetime.Transient, Assert.Single(services, d => d.ServiceType == type).Lifetime);
        foreach (var type in Singletons) Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == type).Lifetime);
    }

    [Fact]
    public void Null_collection_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => MonoReportServiceCollectionExtensions.AddMonoReport(null!));
    }
    [Fact]
    public void Registration_is_lazy_idempotent_and_preserves_every_pre_registered_descriptor()
    {
        var calls = 0;
        var services = new ServiceCollection();
        var descriptors = Transients.Concat(Singletons).Select(type => new ServiceDescriptor(type, _ => { calls++; throw new InvalidOperationException("not resolved"); }, ServiceLifetime.Scoped)).ToArray();
        foreach (var descriptor in descriptors) services.Add(descriptor);
        services.AddMonoReport();
        services.AddMonoReport();
        Assert.Equal(descriptors, services.ToArray());
        Assert.Equal(0, calls);

        var defaults = new ServiceCollection().AddMonoReport();
        var snapshot = defaults.ToArray();
        defaults.AddMonoReport();
        Assert.Equal(snapshot, defaults.ToArray());
    }

    [Fact]
    public void Validated_default_graph_has_exact_instance_lifetimes_across_scopes_and_providers()
    {
        var services = new ServiceCollection().AddMonoReport();
        using var provider = Build(services);
        using var other = Build(services);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        foreach (var type in Transients)
        {
            Assert.NotSame(provider.GetRequiredService(type), provider.GetRequiredService(type));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(type), second.ServiceProvider.GetRequiredService(type));
        }
        foreach (var type in Singletons)
        {
            var instance = provider.GetRequiredService(type);
            Assert.Same(instance, provider.GetRequiredService(type));
            Assert.Same(instance, first.ServiceProvider.GetRequiredService(type));
            Assert.Same(instance, second.ServiceProvider.GetRequiredService(type));
            Assert.NotSame(instance, other.GetRequiredService(type));
        }
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(MergeContext) || d.ServiceType == typeof(MergeOptions) || d.ServiceType == typeof(JsonDocument) || d.ServiceType == typeof(Stream));
        Assert.DoesNotContain(services, d => d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Partial_graph_uses_authoritative_custom_registry_and_standard_after_registration_override()
    {
        var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter>());
        registry.Register("prefix", new PrefixFormatter());
        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddMonoReport();
        using (var provider = Build(services))
        {
            Assert.Same(registry, provider.GetRequiredService<FormatterRegistry>());
            var engine = provider.GetRequiredService<MergeEngine>();
            Assert.Equal("custom:value", Text(Merge(engine, NameTemplate(), "{\"Name\":\"value\"}", PrefixOptions("custom:"))));
            Assert.Throws<TemplateException>(() => Merge(engine, Docx.Create(Docx.P(Docx.Field("MERGEFIELD Amount \\* SpellOut"))), "{\"Amount\":1}"));
        }
        var replacement = new FormatterRegistry(new Dictionary<string, IValueFormatter>());
        replacement.Register("prefix", new PrefixFormatter());
        services.Replace(ServiceDescriptor.Singleton(replacement));
        using var replaced = Build(services);
        Assert.Same(replacement, replaced.GetRequiredService<FormatterRegistry>());
        services.AddSingleton(registry);
        using var last = Build(services);
        Assert.Same(registry, last.GetRequiredService<FormatterRegistry>());
    }

    [Fact]
    public void Singleton_setup_is_visible_to_independently_resolved_engines_and_is_not_reset()
    {
        var services = new ServiceCollection().AddMonoReport();
        using var provider = Build(services);
        var registry = provider.GetRequiredService<FormatterRegistry>();
        registry.Register("prefix", new PrefixFormatter());
        services.AddMonoReport();
        foreach (var engine in new[] { provider.GetRequiredService<MergeEngine>(), provider.GetRequiredService<MergeEngine>() })
            Assert.Equal("shared:value", Text(Merge(engine, NameTemplate(), "{\"Name\":\"value\"}", PrefixOptions("shared:"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scoped_custom_dependencies_are_isolated_used_and_disposed(bool replaceAfter)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddScoped<ScopedFormatter>();
        var registryDescriptor = ServiceDescriptor.Scoped<FormatterRegistry>(p => new FormatterRegistry(new Dictionary<string, IValueFormatter> { ["scoped"] = p.GetRequiredService<ScopedFormatter>() }));
        if (!replaceAfter) services.Add(registryDescriptor);
        services.AddMonoReport();
        if (replaceAfter) services.Replace(registryDescriptor);
        using var provider = Build(services);
        ScopedDependency dependency;
        FormatterRegistry registry;
        using (var scope = provider.CreateScope())
        {
            dependency = scope.ServiceProvider.GetRequiredService<ScopedDependency>();
            registry = scope.ServiceProvider.GetRequiredService<FormatterRegistry>();
            Assert.Same(registry, scope.ServiceProvider.GetRequiredService<FormatterRegistry>());
            var options = new MergeOptions { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Name"] = new("scoped") } };
            var engine = scope.ServiceProvider.GetRequiredService<MergeEngine>();
            Assert.Equal(dependency.Id + ":value", Text(Merge(engine, NameTemplate(), "{\"Name\":\"value\"}", options)));
            Assert.False(dependency.Disposed);
            using var otherScope = provider.CreateScope();
            Assert.NotSame(registry, otherScope.ServiceProvider.GetRequiredService<FormatterRegistry>());
            Assert.NotSame(dependency, otherScope.ServiceProvider.GetRequiredService<ScopedDependency>());
        }
        Assert.True(dependency.Disposed);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MergeEngine>());
    }

    [Fact]
    public void Scoped_builtin_override_is_preserved_but_rejected_on_singleton_factory_resolution()
    {
        var services = new ServiceCollection();
        services.AddScoped<RubSpellOutFormatter>();
        var original = Assert.Single(services);
        services.AddMonoReport();
        Assert.Same(original, Assert.Single(services, d => d.ServiceType == typeof(RubSpellOutFormatter)));
        using var provider = Build(services);
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MergeEngine>());
        Assert.Contains(nameof(RubSpellOutFormatter), error.Message);
        Assert.Contains("scoped", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Di_and_manual_composition_match_sample_content_fields_properties_and_structure()
    {
        using var provider = Build(new ServiceCollection().AddMonoReport());
        var template = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sample.template.docx"));
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample.json"));
        var options = new MergeOptions { Culture = CultureInfo.GetCultureInfo("en-GB") };
        var actual = Merge(provider.GetRequiredService<MergeEngine>(), template, json, options);
        var expected = Docx.Merge(template, json, options);
        Assert.True(XNode.DeepEquals(Docx.Read(expected.Document), Docx.Read(actual.Document)));
        Assert.Equal(expected.Warnings, actual.Warnings);
        Assert.Contains("Northwind", Text(actual));
        Assert.Contains("Сто двадцать пять тысяч", Text(actual));
    }

    [Fact]
    public void Complex_non_bmp_remerge_preserves_codes_distinct_properties_and_scalar_boundaries()
    {
        var w = WordXml.RunElementName.Namespace;
        const string code = "MERGEFIELD Name \\* MERGEFORMAT";
        var bold = new XElement(w + "rPr", new XElement(w + "b"));
        var regular = new XElement(w + "rPr", new XElement(w + "color", new XAttribute(w + "val", "112233")));
        var template = Docx.Create(Docx.P(Docx.ComplexContent(code,
            new XElement(w + "r", bold, new XElement(w + "t", "x")),
            new XElement(w + "r", regular, new XElement(w + "t", "cache")))));
        using var provider = Build(new ServiceCollection().AddMonoReport());
        var engine = provider.GetRequiredService<MergeEngine>();
        foreach (var value in new[] { "😀A🚧B", "🚧Second😀" })
        {
            var result = Merge(engine, template, JsonSerializer.Serialize(new { Name = value }));
            var xml = Docx.Read(result.Document);
            Assert.Equal(value, Docx.Text(xml));
            Assert.Equal(code, Assert.Single(xml.Descendants(w + "instrText")).Value);
            var properties = xml.Descendants(w + "rPr").ToArray();
            Assert.Equal(2, properties.Length);
            Assert.True(XNode.DeepEquals(bold, properties[0]));
            Assert.True(XNode.DeepEquals(regular, properties[1]));
            foreach (var node in xml.Descendants(w + "t"))
                Assert.DoesNotContain(node.Value.EnumerateRunes(), rune => rune.Value == 0xfffd);
            template = result.Document;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Di_preserves_actionable_preflight_rejections(bool nested)
    {
        var template = nested ? Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))) : Docx.Create(Docx.ConflictingLayout("paragraph"));
        using var provider = Build(new ServiceCollection().AddMonoReport());
        using var stream = new MemoryStream(template);
        using var data = JsonDocument.Parse("{\"Name\":\"replacement\",\"A\":[{}],\"B\":[{}]}");
        var error = Assert.Throws<TemplateException>(() => provider.GetRequiredService<MergeEngine>().Merge(stream, data));
        Assert.Contains("/word/document.xml", error.Message);
        Assert.Contains(nested ? "plain cached text" : "separate supported", error.Message);
        Assert.Equal(template, stream.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_merges_isolate_options_values_regions_warnings_and_failures(bool reuseEngine)
    {
        using var provider = Build(new ServiceCollection().AddMonoReport());
        provider.GetRequiredService<FormatterRegistry>().Register("prefix", new PrefixFormatter());
        var commonEngine = provider.GetRequiredService<MergeEngine>();
        var template = ConcurrentTemplate();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 12).Select(async i =>
        {
            var engine = reuseEngine ? commonEngine : provider.GetRequiredService<MergeEngine>();
            await gate.Task;
            var culture = CultureInfo.GetCultureInfo(i % 2 == 0 ? "en-GB" : "de-DE");
            var options = new MergeOptions
            {
                Culture = culture,
                MissingValues = i == 11 ? MissingValueBehavior.Error : i % 2 == 0 ? MissingValueBehavior.Warning : MissingValueBehavior.EmptyString,
                FieldFormatters = new Dictionary<string, FormatterBinding> { ["Name"] = new("prefix", new Dictionary<string, string> { ["prefix"] = $"run-{i}:" }) }
            };
            var json = JsonSerializer.Serialize(new { Name = $"value-{i}😀", Amount = 1234.5m, Items = Enumerable.Range(0, i % 3 + 1).Select(n => new { Item = $"item-{i}-{n}" }).ToArray() });
            using var stream = new MemoryStream(template);
            using var data = JsonDocument.Parse(json);
            if (i == 11)
            {
                var error = Assert.Throws<TemplateException>(() => engine.Merge(stream, data, options));
                Assert.Equal("Missing", error.Field);
            }
            else
            {
                var result = engine.Merge(stream, data, options);
                Docx.AssertValid(result.Document);
                var xml = Docx.Read(result.Document);
                var expectedItems = string.Concat(Enumerable.Range(0, i % 3 + 1).Select(n => $"item-{i}-{n}"));
                Assert.Equal($"run-{i}:value-{i}😀" + 1234.5m.ToString("0.00", culture) + expectedItems + "7", Docx.Text(xml));
                Assert.Equal(i % 2 == 0 ? 1 : 0, result.Warnings.Count);
                if (i % 2 == 0) Assert.Equal("Missing", Assert.Single(result.Warnings).Field);
                var fields = xml.Descendants(WordXml.SimpleFieldElementName).Select(f => (string?)f.Attribute(WordXml.InstructionAttributeName)).ToArray();
                Assert.Contains("MERGEFIELD Name", fields);
                Assert.Contains("MERGEFIELD Amount \\# \"0.00\"", fields);
                Assert.Contains("PAGE", fields);
                Assert.Equal(i % 3 + 1, fields.Count(f => f == "MERGEFIELD Item"));
                Assert.True(XNode.DeepEquals(Docx.Properties(), Assert.Single(xml.Descendants(WordXml.RunPropertiesElementName))));
                Assert.Equal(i % 3 + 1, xml.Descendants(WordXml.TableRowElementName).Count());
            }
            Assert.Equal(template, stream.ToArray());
            Assert.True(stream.CanRead);
        }).ToArray();
        gate.SetResult();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        var recovery = Merge(commonEngine, NameTemplate(), "{\"Name\":\"recovered\"}");
        Assert.Equal("recovered", Text(recovery));
        Assert.Empty(recovery.Warnings);
        var warning = Merge(commonEngine, NameTemplate(), "{}", new MergeOptions { MissingValues = MissingValueBehavior.Warning });
        Assert.Single(warning.Warnings);
        Assert.Empty(Merge(commonEngine, NameTemplate(), "{\"Name\":\"next\"}").Warnings);
    }

    private static ServiceProvider Build(IServiceCollection services) => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    private static byte[] NameTemplate() => Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name")));
    private static string Text(MergeResult result) => Docx.Text(Docx.Read(result.Document));
    private static MergeOptions PrefixOptions(string prefix) => new() { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Name"] = new("prefix", new Dictionary<string, string> { ["prefix"] = prefix }) } };

    private static MergeResult Merge(MergeEngine engine, byte[] template, string json, MergeOptions? options = null)
    {
        using var stream = new MemoryStream(template);
        using var data = JsonDocument.Parse(json);
        var result = engine.Merge(stream, data, options);
        Assert.True(stream.CanRead);
        Assert.Equal(template, stream.ToArray());
        Docx.AssertValid(result.Document);
        return result;
    }

    private static byte[] ConcurrentTemplate() => Docx.Create(
        Docx.P(Docx.Field("MERGEFIELD Name", properties: Docx.Properties())),
        Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\"")),
        Docx.P(Docx.Field("MERGEFIELD Missing")),
        Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:Items"), Docx.Field("MERGEFIELD Item"), Docx.Field("MERGEFIELD TableEnd:Items"))),
        Docx.P(Docx.Field("PAGE", "7")));

    private sealed class PrefixFormatter : IValueFormatter
    {
        public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options) => options["prefix"] + value.GetString();
    }

    private sealed class ScopedDependency : IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ScopedFormatter(ScopedDependency dependency) : IValueFormatter
    {
        public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options) => dependency.Id + ":" + value.GetString();
    }
}
