using MonoReport.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using MonoReport.Services;
using System.Text.Json;
using System.Text;

namespace MonoReport.Tests.Services;

public sealed class CliValidationTests
{
    [Theory]
    [InlineData("valid", 0)]
    [InlineData("invalid", 1)]
    [InlineData("corrupt", 1)]
    [InlineData("empty", 1)]
    public void Validation_writes_one_result_without_reading_stdin_and_disposes_template(string fixture, int exit)
    {
        var bytes = fixture switch
        {
            "valid" => Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing \\* UnknownFormatter"))),
            "invalid" => Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))),
            "corrupt" => Encoding.UTF8.GetBytes("corrupt DOCX"),
            _ => []
        };
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, bytes);
        try
        {
            using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
            var app = new CliApplication(new CliOptionsParser(), provider.GetRequiredService<MergeEngine>(), provider.GetRequiredService<TemplateAnalyzer>());
            using var input = new ThrowingReadStream();
            using var output = new MemoryStream();
            using var errors = new StringWriter();
            Assert.Equal(exit, app.Run(["--analyze", "--template", path], input, output, errors));
            Assert.Empty(errors.ToString());
            Assert.True(input.CanRead);
            Assert.True(output.CanWrite);
            using var result = JsonDocument.Parse(output.ToArray());
            Assert.Equal(exit == 0, result.RootElement.GetProperty("valid").GetBoolean());
            Assert.Equal(4, result.RootElement.EnumerateObject().Count());
            Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("fields").ValueKind);
            Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("warnings").ValueKind);
            if (fixture == "valid")
            {
                var field = Assert.Single(result.RootElement.GetProperty("fields").EnumerateArray());
                Assert.True(field.TryGetProperty("formats", out var formats));
                Assert.Empty(Assert.Single(formats.EnumerateArray()).EnumerateArray());
                Assert.False(field.TryGetProperty("switches", out _));
            }
            if (exit == 1)
            {
                var issue = result.RootElement.GetProperty("errors")[0];
                Assert.Equal(new[] { "code", "field", "location", "message", "part" }, issue.EnumerateObject().Select(p => p.Name).Order().ToArray());
            }
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(bytes.Length, exclusive.Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Operational_failure_uses_safe_stderr_and_empty_stdout()
    {
        using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
        var app = new CliApplication(new CliOptionsParser(), provider.GetRequiredService<MergeEngine>(), provider.GetRequiredService<TemplateAnalyzer>());
        using var output = new MemoryStream();
        using var errors = new StringWriter();
        Assert.Equal(1, app.Run(["--analyze", "-t", "/nonexistent/private-template.docx"], Stream.Null, output, errors));
        Assert.Empty(output.ToArray());
        Assert.Contains("could not be completed", errors.ToString());
        Assert.DoesNotContain("private-template", errors.ToString());
    }

    [Fact]
    public void Output_failure_is_execution_failure_and_template_handle_is_released()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))));
        try
        {
            using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
            var app = new CliApplication(new CliOptionsParser(), provider.GetRequiredService<MergeEngine>(), provider.GetRequiredService<TemplateAnalyzer>());
            using var output = new ThrowingWriteStream();
            using var errors = new StringWriter();
            Assert.Equal(1, app.Run(["--analyze", "-t", path], Stream.Null, output, errors));
            Assert.NotEmpty(errors.ToString());
            Assert.True(output.CanWrite);
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    private sealed class ThrowingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("stdin must remain unread");
        public override int Read(Span<byte> buffer) => throw new InvalidOperationException("stdin must remain unread");
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("private output failure");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("private output failure");
    }
    [Theory]
    [InlineData("--output", "-")]
    [InlineData("-o", "result.docx")]
    [InlineData("--json", "{}")]
    [InlineData("--json-file", "data.json")]
    [InlineData("--culture", "en-US")]
    [InlineData("--culture", "")]
    public void Validation_rejects_generation_options_in_either_order(string option, string value)
    {
        var parser = new CliOptionsParser();
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze", "--template", "template.docx", option, value]));
        Assert.Throws<ArgumentException>(() => parser.Parse([option, value, "--template", "template.docx", "--analyze"]));
    }

    [Fact]
    public void Validation_requires_template_and_rejects_duplicate_flags_aliases_and_stdin()
    {
        var parser = new CliOptionsParser();
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze"]));
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze", "-t", "a", "--template", "b"]));
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze", "-t", "a", "--analyze"]));
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze", "-t", "a", "--stdin"]));
        Assert.Throws<ArgumentException>(() => parser.Parse(["--analyze", "-t", "a", "value"]));
    }

    [Fact]
    public void Validation_is_valueless_and_generation_flag_looking_values_do_not_select_mode()
    {
        var parser = new CliOptionsParser();
        Assert.True(parser.Parse(["--analyze", "-t", "a"]).Analyze);
        Assert.False(parser.Parse(["-t", "--analyze", "--json", "{}"]).Analyze);
        Assert.False(parser.Parse(["-t", "a", "--json", "--analyze"]).Analyze);
        Assert.True(parser.Parse(["--analyze", "--unknown", "--help"]).Help);
    }
}
