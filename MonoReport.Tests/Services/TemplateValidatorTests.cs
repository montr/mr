using Microsoft.Extensions.DependencyInjection;
using MonoReport.Models;
using MonoReport.Services;
using DocumentFormat.OpenXml.Packaging;
using System.Text;
using System.Xml.Linq;

namespace MonoReport.Tests.Services;

public sealed class TemplateValidatorTests
{
    [Fact]
    public void Validates_without_data_and_retains_stream_ownership()
    {
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        using var stream = new MemoryStream(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Absent"))));
        var result = provider.GetRequiredService<TemplateValidator>().Validate(stream);
        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("MERGEFIELD", "template.invalid_field_syntax")]
    [InlineData("MERGEFIELD TableEnd:Items", "template.invalid_region")]
    public void Shares_structural_codes_with_generation(string code, string expected)
    {
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        var bytes = Docx.Create(Docx.P(Docx.Field(code)));
        using var input = new MemoryStream(bytes);
        var issue = Assert.Single(provider.GetRequiredService<TemplateValidator>().Validate(input).Errors);
        Assert.Equal(expected, issue.Code);
        Assert.Equal("/word/document.xml", issue.Part);
        using var generation = new MemoryStream(bytes);
        using var json = System.Text.Json.JsonDocument.Parse("{}");
        var error = Assert.Throws<TemplateException>(() => provider.GetRequiredService<MergeEngine>().Merge(generation, json));
        Assert.Equal(issue.Code, error.Code);
    }

    [Fact]
    public void Invalid_package_is_a_result()
    {
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        using var input = new MemoryStream([1, 2, 3]);
        var result = provider.GetRequiredService<TemplateValidator>().Validate(input);
        Assert.False(result.Valid);
        Assert.Equal("template.invalid_package", Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("/word/document.xml")]
    [InlineData("/word/header1.xml")]
    [InlineData("/word/footer1.xml")]
    [InlineData("/word/footnotes.xml")]
    [InlineData("/word/endnotes.xml")]
    public void Checks_every_supported_story_in_deterministic_order(string part)
    {
        var result = Validate(Docx.CreateStories(part, Docx.P(Docx.Field("MERGEFIELD"))));
        var issue = Assert.Single(result.Errors);
        Assert.Equal("template.invalid_field_syntax", issue.Code);
        Assert.Equal(part, issue.Part);
    }

    [Theory]
    [InlineData("syntax", "template.invalid_field_syntax")]
    [InlineData("structure", "template.invalid_field_structure")]
    [InlineData("region", "template.invalid_region")]
    [InlineData("overlap", "template.overlapping_regions")]
    [InlineData("nested", "template.unsupported_nested_field")]
    public void Reports_every_structural_category_from_shared_rules(string category, string code)
    {
        var content = category switch
        {
            "syntax" => [Docx.P(Docx.Field("MERGEFIELD"))],
            "structure" => [Docx.P(new XElement(WordXml.RunElementName, Docx.Character("end")))],
            "region" => [Docx.P(Docx.Field("MERGEFIELD TableEnd:Items"))],
            "overlap" => RegionTests.ConflictingLayout("paragraph"),
            _ => [Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))]
        };
        var issue = Assert.Single(Validate(Docx.Create(content)).Errors);
        Assert.Equal(code, issue.Code);
    }

    [Fact]
    public void Collects_independent_same_story_errors_and_generation_throws_first_shared_error()
    {
        var content = new List<XElement>
        {
            Docx.P(Docx.Field("MERGEFIELD")),
            Docx.P(Docx.Field("MERGEFIELD \\#")),
            Docx.P(Docx.NestedResult(false, false, "MERGEFIELD First", "PAGE")),
            Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Second", "PAGE")),
            Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:BoundaryA"))),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:BoundaryA")),
            Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:BoundaryB"))),
            Docx.P(Docx.Field("MERGEFIELD TableEnd:BoundaryB"))
        };
        content.AddRange(RegionTests.ConflictingLayout("paragraph", "OverlapB"));
        content.AddRange(RegionTests.ConflictingLayout("paragraph", "OverlapD"));
        var bytes = Docx.Create(content.ToArray());

        var result = Validate(bytes);
        Assert.Equal(new[]
        {
            "template.invalid_field_syntax", "template.invalid_field_syntax",
            "template.unsupported_nested_field", "template.unsupported_nested_field",
            "template.invalid_region", "template.invalid_region",
            "template.overlapping_regions", "template.overlapping_regions"
        }, result.Errors.Select(error => error.Code));
        Assert.Equal(result.Errors.Count, result.Errors.DistinctBy(error =>
            (error.Code, error.Field, error.Part, error.Location)).Count());

        using var input = new MemoryStream(bytes);
        using var data = System.Text.Json.JsonDocument.Parse("{}");
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        var error = Assert.Throws<TemplateException>(() => provider.GetRequiredService<MergeEngine>().Merge(input, data));
        Assert.Equal(result.Errors[0].Code, error.Code);
        Assert.Equal(result.Errors[0].Field, error.Field);
    }

    [Fact]
    public void Reports_all_sibling_nested_fields_in_one_cached_result_in_document_order()
    {
        var outer = Docx.ComplexContent("MERGEFIELD Outer",
            Docx.Field("PAGE", "1"),
            Docx.Field("MERGEFIELD Inner", "inner"),
            Docx.Field("IF 1 = 1 yes no", "yes"));
        var bytes = Docx.Create(Docx.P(outer));

        var result = Validate(bytes);
        Assert.Equal(3, result.Errors.Count);
        Assert.All(result.Errors, issue =>
        {
            Assert.Equal("template.unsupported_nested_field", issue.Code);
            Assert.Equal("Outer", issue.Field);
            Assert.Equal("/word/document.xml", issue.Part);
        });
        var locations = result.Errors.Select(issue => issue.Location).ToArray();
        Assert.Equal(new string?[]
        {
            "/word/document.xml, paragraph 1, field occurrence 7",
            "/word/document.xml, paragraph 1, field occurrence 10",
            "/word/document.xml, paragraph 1, field occurrence 13"
        }, locations);
        Assert.Equal(result.Errors.Count, result.Errors.DistinctBy(issue =>
            (issue.Code, issue.Field, issue.Part, issue.Location)).Count());

        using var input = new MemoryStream(bytes);
        using var data = System.Text.Json.JsonDocument.Parse("{}");
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        var error = Assert.Throws<TemplateException>(() => provider.GetRequiredService<MergeEngine>().Merge(input, data));
        Assert.Equal(result.Errors[0].Code, error.Code);
        Assert.Equal(result.Errors[0].Field, error.Field);
        Assert.Equal(result.Errors[0].Location, error.Location);
        Assert.Contains("nested PAGE", error.Message);
    }

    [Fact]
    public void Corrupt_field_state_stops_dependent_story_checks_but_other_stories_continue()
    {
        var invalid = Docx.P(new XElement(WordXml.RunElementName, Docx.Character("end")),
            Docx.Field("MERGEFIELD"), Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"));
        var bytes = Docx.CreateStories("/word/document.xml", invalid);
        using var stream = new MemoryStream(bytes);
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var header = document.MainDocumentPart!.HeaderParts.Single();
            using var target = header.GetStream(FileMode.Create, FileAccess.Write);
            target.Write(Encoding.UTF8.GetBytes("<broken"));
        }
        var result = Validate(stream.ToArray());
        Assert.Equal(new[] { "template.invalid_field_structure", "template.invalid_xml" },
            result.Errors.Select(error => error.Code));
    }

    [Fact]
    public void Honors_current_position_nonseekable_ownership_and_content_preservation()
    {
        var bytes = Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing \\* UnknownFormatter")));
        var prefix = Encoding.UTF8.GetBytes("prefix");
        using var inner = new MemoryStream(prefix.Concat(bytes).ToArray());
        inner.Position = prefix.Length;
        using var stream = new NonSeekableReadStream(inner);
        var result = Validator().Validate(stream);
        Assert.True(result.Valid);
        Assert.True(stream.CanRead);
        Assert.Equal(prefix.Concat(bytes), inner.ToArray());
        Assert.Equal(inner.Length, inner.Position);
    }

    [Fact]
    public void Rejects_null_and_unreadable_streams_and_propagates_operational_io()
    {
        var validator = Validator();
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
        using var unreadable = new UnreadableStream();
        Assert.Throws<ArgumentException>(() => validator.Validate(unreadable));
        using var failing = new ThrowingReadStream();
        Assert.Throws<IOException>(() => validator.Validate(failing));
    }

    [Fact]
    public async Task Reusable_validator_is_concurrent_and_never_evaluates_data_or_formatters()
    {
        var validator = Validator();
        var bytes = Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing \\* DefinitelyUnavailable")));
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes);
            return validator.Validate(stream);
        })));
        Assert.All(results, result => { Assert.True(result.Valid); Assert.Empty(result.Errors); });
    }

    private static TemplateValidationResult Validate(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Validator().Validate(stream);
    }

    private static TemplateValidator Validator()
    {
        var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        return provider.GetRequiredService<TemplateValidator>();
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    private sealed class ThrowingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("operational read failure");
        public override int Read(Span<byte> buffer) => throw new IOException("operational read failure");
    }
}
