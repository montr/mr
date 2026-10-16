using System.Globalization;
using System.Text.Json;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services;

public sealed class GenerationDiagnosticsTests
{
    [Theory]
    [InlineData("MERGEFIELD Value", null)]
    [InlineData("MERGEFIELD Value \\# \"0.00\"", null)]
    [InlineData("MERGEFIELD Value \\@ \"yyyy-MM-dd\"", null)]
    [InlineData("MERGEFIELD Value \\* SpellOut", null)]
    [InlineData("MERGEFIELD Value", "custom")]
    public void Empty_string_skips_value_formatting_and_renders_empty(string code, string? custom)
    {
        var observer = new ObservingFormatter();
        var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter>
        {
            [RubSpellOutFormatter.FormatterName] = new RubSpellOutFormatter(),
            ["custom"] = observer
        });
        var options = custom is null ? null : new MergeOptions
        {
            FieldFormatters = new Dictionary<string, FormatterBinding> { ["Value"] = new(custom) }
        };

        var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), "{\"Value\":\"\"}", options, registry);

        Assert.Equal(string.Empty, Docx.Text(Docx.Read(result.Document)));
        Assert.Equal(0, observer.Calls);
    }

    [Fact]
    public void Empty_values_still_detect_unknown_registered_and_word_formatters()
    {
        var empty = new FormatterRegistry(new Dictionary<string, IValueFormatter>());
        var custom = Assert.Throws<TemplateException>(() => Docx.Merge(
            Docx.Create(Docx.P(Docx.Field("MERGEFIELD Value"))), "{\"Value\":\"\"}",
            new MergeOptions { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Value"] = new("missing") } }, empty));
        Assert.Equal("generation.unknown_formatter", custom.Code);

        var word = Assert.Throws<TemplateException>(() => Docx.Merge(
            Docx.Create(Docx.P(Docx.Field("MERGEFIELD Value \\z Nope"))), "{\"Value\":\"\"}"));
        Assert.Equal("generation.invalid_format", word.Code);

        foreach (var code in new[] { "MERGEFIELD Value \\# Q", "MERGEFIELD Value \\@ %" })
        {
            var pattern = Assert.Throws<TemplateException>(() => Docx.Merge(
                Docx.Create(Docx.P(Docx.Field(code))), "{\"Value\":\"\"}"));
            Assert.Equal("generation.invalid_format", pattern.Code);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Items\":null}")]
    [InlineData("{\"Items\":\"\"}")]
    public void Missing_null_and_empty_string_collections_expand_to_zero_items(string json)
    {
        var template = Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD TableStart:Items")),
            Docx.P(Docx.Field("MERGEFIELD Name")),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")));

        var result = Docx.Merge(template, json);

        Assert.Equal(string.Empty, Docx.Text(Docx.Read(result.Document)));
    }

    [Fact]
    public void Aggregates_nested_item_errors_in_encounter_order_without_deduplication()
    {
        var template = Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD TableStart:Orders")),
            Docx.P(Docx.Field("MERGEFIELD TableStart:Items")),
            Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\"")),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:Orders")),
            Docx.P(Docx.Field("MERGEFIELD Final \\@ \"yyyy-MM-dd\"")));
        const string json = """{"Orders":[{"Items":[{"Amount":"bad"},{"Amount":"bad"}]},{"Items":[{"Amount":"bad"}]}],"Final":"bad"}""";

        var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, json));

        Assert.Equal(4, error.Errors.Count);
        Assert.False(error.LimitReached);
        Assert.Equal(new[]
        {
            "Orders[0].Items[0].Amount", "Orders[0].Items[1].Amount",
            "Orders[1].Items[0].Amount", "Final"
        }, error.Errors.Select(item => item.DataPath));
        Assert.All(error.Errors, item => Assert.Equal("generation.invalid_format", item.Code));
        Assert.All(error.Errors, item => Assert.StartsWith("/word/document.xml", item.Location));
    }

    [Fact]
    public void Aggregates_runtime_errors_across_stories_in_analyzer_order()
    {
        var template = Docx.CreateStories(null);
        const string json = """{"Name":"bad","Empty":[],"Items":[]}""";

        var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, json));

        Assert.Equal(new[]
        {
            "/word/document.xml", "/word/header1.xml", "/word/footer1.xml",
            "/word/footnotes.xml", "/word/endnotes.xml"
        }, error.Errors.Select(item => item.Part));
        Assert.All(error.Errors, item => Assert.Equal("Name", item.DataPath));
    }

    [Fact]
    public void Missing_value_error_policy_aggregates_while_warning_policy_still_succeeds()
    {
        var template = Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD First")),
            Docx.P(Docx.Field("MERGEFIELD Second")));
        var failed = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{}",
            new MergeOptions { MissingValues = MissingValueBehavior.Error }));
        Assert.Equal(new[] { "First", "Second" }, failed.Errors.Select(item => item.DataPath));

        var warning = Docx.Merge(template, "{}", new MergeOptions { MissingValues = MissingValueBehavior.Warning });
        Assert.Equal(2, warning.Warnings.Count);
        Assert.Equal(string.Empty, Docx.Text(Docx.Read(warning.Document)));
    }

    [Theory]
    [InlineData(99, 99, false)]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, true)]
    public void Error_limit_stops_at_100_without_invoking_a_101st_formatter(int fields, int expected, bool limitReached)
    {
        var formatter = new ThrowingFormatter();
        var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter> { ["fail"] = formatter });
        var template = Docx.Create(Enumerable.Range(0, fields)
            .Select(_ => Docx.P(Docx.Field("MERGEFIELD Value"))).ToArray());
        var options = new MergeOptions
        {
            FieldFormatters = new Dictionary<string, FormatterBinding> { ["Value"] = new("fail") }
        };

        var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{\"Value\":1}", options, registry));

        Assert.Equal(expected, error.Errors.Count);
        Assert.Equal(expected, formatter.Calls);
        Assert.Equal(limitReached, error.LimitReached);
    }

    [Fact]
    public void Unexpected_custom_formatter_failure_is_fatal_and_not_aggregated()
    {
        var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter> { ["fatal"] = new FatalFormatter() });
        var options = new MergeOptions
        {
            FieldFormatters = new Dictionary<string, FormatterBinding> { ["Value"] = new("fatal") }
        };

        Assert.Throws<IOException>(() => Docx.Merge(
            Docx.Create(Docx.P(Docx.Field("MERGEFIELD Value")), Docx.P(Docx.Field("MERGEFIELD Value"))),
            "{\"Value\":1}", options, registry));
    }

    private sealed class ObservingFormatter : IValueFormatter
    {
        public int Calls { get; private set; }
        public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options)
        {
            Calls++;
            return "formatted";
        }
    }

    private sealed class ThrowingFormatter : IValueFormatter
    {
        public int Calls { get; private set; }
        public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options)
        {
            Calls++;
            throw new FormatException("expected test failure");
        }
    }

    private sealed class FatalFormatter : IValueFormatter
    {
        public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options) =>
            throw new IOException("fatal formatter failure");
    }
}
