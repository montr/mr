using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MonoReport.Api.Services;
using MonoReport.Models;
using System.Globalization;
using MonoReport.Services;
using System.IO.Compression;
using System.Xml.Linq;

namespace MonoReport.Tests.Services;

public sealed class HttpApiTests
{
    [Fact]
    public async Task Validation_collects_independent_story_errors_in_deterministic_order()
    {
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        using var form = Form(MultiStoryErrors()); using var response = await client.PostAsync("/api/documents/analyze", form); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Equal(new[] { "template.invalid_region", "template.invalid_field_syntax" }, errors.Select(x => x.GetProperty("code").GetString()).ToArray());
        Assert.Equal(new[] { "/word/document.xml", "/word/header1.xml" }, errors.Select(x => x.GetProperty("part").GetString()).ToArray());
    }

    internal static byte[] MultiStoryErrors()
    {
        using var stream = new MemoryStream(); stream.Write(Docx.CreateStories("/word/header1.xml", Docx.P(Docx.Field("MERGEFIELD")))); stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = archive.GetEntry("word/document.xml")!;
            XDocument xml; using (var input = entry.Open()) xml = XDocument.Load(input);
            xml.Root!.Element(WordXml.BodyElementName)!.AddFirst(Docx.P(Docx.Field("MERGEFIELD TableEnd:Other")));
            entry.Delete(); using var output = archive.CreateEntry("word/document.xml").Open(); xml.Save(output);
        }
        return stream.ToArray();
    }

    [Theory]
    [InlineData("xml", "template.invalid_xml")]
    [InlineData("structure", "template.unsupported_structure")]
    public async Task Package_part_errors_have_matching_validation_and_generation_contracts(string kind, string expected)
    {
        using var bytes = new MemoryStream(); bytes.Write(Docx.Create(Docx.P(Docx.Run("ok")))); bytes.Position = 0;
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Update, true))
        {
            var entry = archive.GetEntry("word/document.xml")!; entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open());
            writer.Write(kind == "xml" ? "<broken" : new XElement(WordXml.DocumentElementName).ToString());
        }
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        foreach (var endpoint in new[] { "analyze", "generate" })
        {
            using var form = Form(bytes.ToArray(), endpoint == "generate" ? "{}" : null);
            using var response = await client.PostAsync($"/api/documents/{endpoint}", form);
            Assert.Equal(endpoint == "generate" ? 422 : 200, (int)response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var code = endpoint == "generate" ? json.RootElement.GetProperty("code").GetString() : Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString();
            Assert.Equal(expected, code);
        }
    }

    [Fact]
    public async Task Collection_generation_preserves_repeat_content_and_formatting_in_valid_open_xml()
    {
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableStart:Items")), Docx.P(Docx.Field("MERGEFIELD Name", properties: Docx.Properties())), Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")));
        using var form = Form(template, "{\"Items\":[{\"Name\":\"one\"},{\"Name\":\"two\"}]}");
        using var response = await client.PostAsync("/api/documents/generate", form); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsByteArrayAsync(); Docx.AssertValid(result);
        var xml = Docx.Read(result); Assert.Equal("onetwo", Docx.Text(xml));
        Assert.Equal(2, xml.Descendants(WordXml.RunPropertiesElementName).Count());
    }

    [Theory]
    [InlineData("analyze")]
    [InlineData("generate")]
    public async Task Unexpected_service_failures_are_sanitized_on_both_endpoints(string endpoint)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddTransient<TemplateAnalyzer>(_ => throw new IOException("SECRET /private/server/file"))));
        using var client = factory.CreateClient();
        using var form = Form(Docx.Create(Docx.P(Docx.Run("ok"))), endpoint == "generate" ? "{}" : null);
        using var response = await client.PostAsync($"/api/documents/{endpoint}", form);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("server.internal_error", body); Assert.DoesNotContain("SECRET", body); Assert.DoesNotContain("/private/server", body);
    }

    [Fact]
    public async Task Concurrent_request_contexts_and_large_text_field_have_no_incidental_lower_limit()
    {
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            using var form = Form(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))), $"{{\"Name\":\"value{index}\"}}");
            using var response = await client.PostAsync("/api/documents/generate", form); response.EnsureSuccessStatusCode();
            Assert.Equal($"value{index}", Docx.Text(Docx.Read(await response.Content.ReadAsByteArrayAsync())));
        }));
        using var large = Form(Docx.Create(Docx.P(Docx.Run("ok"))), "{\"unused\":\"" + new string('a', 4 * 1024 * 1024 + 1) + "\"}");
        using var result = await client.PostAsync("/api/documents/generate", large); result.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("syntax", "template.invalid_field_syntax")]
    [InlineData("structure", "template.invalid_field_structure")]
    [InlineData("region", "template.invalid_region")]
    [InlineData("overlap", "template.overlapping_regions")]
    [InlineData("nested", "template.unsupported_nested_field")]
    public async Task Validation_and_generation_use_the_same_structural_codes(string kind, string expected)
    {
        var template = kind switch
        {
            "syntax" => Docx.Create(Docx.P(Docx.Field("MERGEFIELD"))),
            "structure" => Docx.Create(Docx.P(new System.Xml.Linq.XElement(WordXml.RunElementName, Docx.Character("end")))),
            "region" => Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableEnd:Items"))),
            "overlap" => Docx.Create(Docx.ConflictingLayout("paragraph")),
            _ => Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE")))
        };
        if (kind == "region")
        {
            var evidence = Path.Combine(Path.GetTempPath(), "feature003-evidence");
            Directory.CreateDirectory(evidence);
            await File.WriteAllBytesAsync(Path.Combine(evidence, "invalid-template.docx"), template);
        }
        using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
        using var form = Form(template); using var response = await client.PostAsync("/api/documents/analyze", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(expected, Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString());
        using var generate = Form(template, "{}"); using var failure = await client.PostAsync("/api/documents/generate", generate);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failure.StatusCode);
        using var problem = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
        Assert.Equal(expected, problem.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(null, "1234.50")]
    [InlineData("", "1234.50")]
    [InlineData("  ", "1234.50")]
    [InlineData("en-GB", "1234.50")]
    [InlineData("de-DE", "1234,50")]
    public async Task Generation_returns_valid_raw_docx_and_invariant_or_explicit_culture(string? culture, string expected)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE");
        using var form = Form(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\""))), "{\"Amount\":1234.5}");
        if (culture is not null) form.Add(new StringContent(culture), "culture");
        using var response = await client.PostAsync("/api/documents/generate", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("document.docx", response.Content.Headers.ContentDisposition!.ToString());
        Assert.False(response.Headers.Contains("X-MonoReport-Warning-Count"));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Docx.AssertValid(bytes);
        Assert.Equal(expected, Docx.Text(Docx.Read(bytes)));
    }

    [Theory]
    [InlineData("analyze")]
    [InlineData("generate")]
    public async Task Request_contract_categories_have_safe_problem_details(string endpoint)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var bytes = Docx.Create(Docx.P(Docx.Run("plain")));
        async Task Check(HttpContent content, int status, string code)
        {
            using (content)
            using (var response = await client.PostAsync($"/api/documents/{endpoint}", content))
            {
                Assert.Equal(status, (int)response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
                Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
                foreach (var key in new[] { "type", "title", "detail", "traceId" }) Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty(key).GetString()));
            }
        }
        await Check(new StringContent("{}"), 415, "request.unsupported_media_type");
        var malformed = new StringContent("bad"); malformed.Headers.ContentType = new("multipart/form-data");
        await Check(malformed, 400, "request.invalid_multipart");
        var broken = new StringContent("not a multipart body"); broken.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=test");
        await Check(broken, 400, "request.invalid_multipart");
        var missing = new MultipartFormDataContent(); missing.Add(new StringContent(string.Empty), "culture");
        await Check(missing, 400, "request.missing_input");
        await Check(Form([], endpoint == "generate" ? "{}" : null), 400, "request.empty_input");
        var duplicate = Form(bytes, endpoint == "generate" ? "{}" : null); duplicate.Add(new ByteArrayContent(bytes), "template", "second.docx");
        await Check(duplicate, 400, "request.duplicate_input");
        var wrong = new MultipartFormDataContent(); wrong.Add(new StringContent("path.docx"), "template");
        if (endpoint == "generate") wrong.Add(new StringContent("{}"), "data");
        await Check(wrong, 400, "request.invalid_input");
        if (endpoint == "analyze")
        {
            foreach (var extra in new[] { "data", "culture", "unknown" })
            {
                var unexpected = Form(bytes); unexpected.Add(new StringContent("x"), extra);
                await Check(unexpected, 400, "request.unexpected_input");
            }
        }
        else
        {
            await Check(Form(bytes), 400, "request.missing_input");
            await Check(Form(bytes, " "), 400, "request.empty_input");
            foreach (var name in new[] { "data", "culture" })
            {
                var fileText = Form(bytes, name == "data" ? null : "{}"); fileText.Add(new ByteArrayContent([1]), name, "x");
                await Check(fileText, 400, "request.invalid_input");
                var twice = Form(bytes, "{}"); twice.Add(new StringContent("x"), name); if (name == "culture") twice.Add(new StringContent("y"), name);
                await Check(twice, 400, "request.duplicate_input");
            }
            foreach (var data in new[] { "bad", "{}{}", new string('[', 65) + "0" + new string(']', 65) })
                await Check(Form(bytes, data), 400, "data.invalid_json");
            var culture = Form(bytes, "{}"); culture.Add(new StringContent("x_invalid_!"), "culture");
            await Check(culture, 400, "culture.invalid");
        }
    }

    [Theory]
    [InlineData("MERGEFIELD Items", "{\"Items\":[]}", "generation.invalid_data")]
    [InlineData("MERGEFIELD Amount \\# \"0.00\"", "{\"Amount\":\"oops\"}", "generation.invalid_format")]
    [InlineData("MERGEFIELD Amount \\* SpellOut", "{\"Amount\":1}", "generation.unknown_formatter")]
    [InlineData("MERGEFIELD TableStart:Items", "{\"Items\":[]}", "template.invalid_region")]
    public async Task Generation_typed_codes_map_to_422(string field, string data, string code)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            if (code == "generation.unknown_formatter") b.ConfigureServices(s => s.AddSingleton(new FormatterRegistry(new Dictionary<string, IValueFormatter>())));
        }); using var client = factory.CreateClient();
        using var form = Form(Docx.Create(Docx.P(Docx.Field(field))), data);
        using var response = await client.PostAsync("/api/documents/generate", form);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Generation_returns_ordered_errors_with_first_error_compatibility_and_data_paths()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var template = Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\"")),
            Docx.P(Docx.Field("MERGEFIELD Date \\@ \"yyyy-MM-dd\"")));
        using var form = Form(template, "{\"Amount\":\"bad\",\"Date\":\"bad\"}");

        using var response = await client.PostAsync("/api/documents/generate", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("generation.invalid_format", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Amount", json.RootElement.GetProperty("field").GetString());
        Assert.False(json.RootElement.GetProperty("limitReached").GetBoolean());
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Amount", "Date" }, errors.Select(item => item.GetProperty("field").GetString()));
        Assert.Equal(new[] { "Amount", "Date" }, errors.Select(item => item.GetProperty("dataPath").GetString()));
        Assert.All(errors, item => Assert.Equal("/word/document.xml", item.GetProperty("part").GetString()));
    }

    [Fact]
    public async Task Generation_returns_all_structural_errors_without_rendering()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var form = Form(Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD")),
            Docx.P(Docx.Field("MERGEFIELD"))), "{}");

        using var response = await client.PostAsync("/api/documents/generate", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Equal(2, errors.Length);
        Assert.All(errors, item => Assert.Equal("template.invalid_field_syntax", item.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Generation_exposes_100_error_limit_without_a_101st_error()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var template = Docx.Create(Enumerable.Range(0, 101)
            .Select(_ => Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\""))).ToArray());
        using var form = Form(template, "{\"Amount\":\"bad\"}");

        using var response = await client.PostAsync("/api/documents/generate", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("limitReached").GetBoolean());
        Assert.Equal(100, json.RootElement.GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task Actual_warning_header_and_unexpected_failure_are_safe_and_recoverable()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<GenerationOptionsFactory>(new TestOptions())));
        using var client = factory.CreateClient();
        using var missing = Form(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing"))), "{}");
        using var warning = await client.PostAsync("/api/documents/generate", missing);
        Assert.Equal(HttpStatusCode.OK, warning.StatusCode);
        Assert.Equal("1", Assert.Single(warning.Headers.GetValues("X-MonoReport-Warning-Count")));
        using var failure = Form(Docx.Create(Docx.P(Docx.Run("ok"))), "{}");
        using var failed = await client.PostAsync("/api/documents/generate", failure);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET", body);
        Assert.Contains("server.internal_error", body);
        using var recovery = Form(Docx.Create(Docx.P(Docx.Run("ok"))));
        using var recovered = await client.PostAsync("/api/documents/analyze", recovery);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    private sealed class TestOptions : GenerationOptionsFactory
    {
        private int _calls;
        public override MergeOptions Create(CultureInfo culture)
        {
            if (Interlocked.Increment(ref _calls) % 2 == 0) throw new IOException("SECRET /server/private/path");
            return new() { Culture = culture, MissingValues = MissingValueBehavior.Warning };
        }
    }

    [Theory]
    [InlineData("arbitrary wording")]
    [InlineData("SECRET request.missing_input /private/internal")]
    public async Task Production_mapping_uses_typed_code_independently_of_exception_wording(string message)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<GenerationOptionsFactory>(new CodedFailureOptions(message))));
        using var client = factory.CreateClient(); using var form = Form(Docx.Create(Docx.P(Docx.Run("ok"))), "{}");
        using var response = await client.PostAsync("/api/documents/generate", form);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("generation.invalid_format", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("A value cannot be formatted as requested.", json.RootElement.GetProperty("detail").GetString());
    }

    private sealed class CodedFailureOptions(string message) : GenerationOptionsFactory
    {
        public override MergeOptions Create(CultureInfo culture) => throw new TemplateException(message, code: "generation.invalid_format");
    }

    [Fact]
    public async Task Validation_is_independent_and_returns_complete_json_shape()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var form = Form(Docx.Create(
            Docx.P(Docx.Field("MERGEFIELD Missing \\* Upper")),
            Docx.P(Docx.Field("MERGEFIELD TableStart:Items")),
            Docx.P(Docx.Field("MERGEFIELD Product.Name")),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:Items"))));
        using var response = await client.PostAsync("/api/documents/analyze", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("valid").GetBoolean());
        Assert.Empty(result.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Empty(result.RootElement.GetProperty("warnings").EnumerateArray());
        var fields = result.RootElement.GetProperty("fields");
        Assert.Equal(new[] { "Missing", "Items" }, fields.EnumerateArray().Select(field => field.GetProperty("name").GetString()));
        Assert.False(fields[0].TryGetProperty("fields", out _));
        Assert.Equal("general", fields[0].GetProperty("formats")[0][0].GetProperty("kind").GetString());
        Assert.Empty(fields[1].GetProperty("formats").EnumerateArray());
        Assert.Equal("Product.Name", fields[1].GetProperty("fields")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Removed_validation_route_is_not_available()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var form = Form(Docx.Create(Docx.P(Docx.Run("plain"))));
        using var response = await client.PostAsync("/api/documents/validate", form);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_package_is_result_for_validation_and_problem_for_generation()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        foreach (var endpoint in new[] { "analyze", "generate" })
        {
            using var form = Form([1, 2, 3], endpoint == "generate" ? "{}" : null);
            using var response = await client.PostAsync($"/api/documents/{endpoint}", form);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (endpoint == "analyze")
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.False(json.RootElement.GetProperty("valid").GetBoolean());
                var issue = Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray());
                Assert.Equal("template.invalid_package", issue.GetProperty("code").GetString());
                foreach (var property in new[] { "message", "field", "part", "location" }) Assert.True(issue.TryGetProperty(property, out _));
            }
            else
            {
                Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
                Assert.Equal("template.invalid_package", json.RootElement.GetProperty("code").GetString());
            }
        }
    }

    internal static MultipartFormDataContent Form(byte[] template, string? data = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(template);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "template", "template.docx");
        if (data is not null) form.Add(new StringContent(data), "data");
        return form;
    }
}
