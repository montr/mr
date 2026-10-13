using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Tests.Services;

public sealed class CliProcessTests
{
    [Theory]
    [InlineData("file")]
    [InlineData("stdout")]
    [InlineData("stdin")]
    public async Task Real_entry_point_generates_valid_sample_preserving_content_codes_and_properties(string mode)
    {
        var directory = EvidenceDirectory();
        var template = Path.Combine(AppContext.BaseDirectory, "sample.template.docx");
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "sample.json");
        var output = Path.Combine(directory, "sample.docx");
        var arguments = new List<string> { "--template", template, "--culture", "en-GB" };
        if (mode == "stdin") arguments.Add("--stdin");
        else arguments.AddRange(["--json-file", jsonPath]);
        if (mode == "file") arguments.AddRange(["--output", output]);
        var json = File.ReadAllText(jsonPath);
        var before = File.ReadAllBytes(template);
        var result = await Run(directory, arguments, mode == "stdin" ? json : null);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        var actual = mode == "file" ? File.ReadAllBytes(output) : result.Output;
        if (mode == "file") Assert.Empty(result.Output);
        else File.WriteAllBytes(output, actual);
        Docx.AssertValid(actual);
        var expected = Docx.Merge(before, json, new() { Culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB") });
        Assert.True(XNode.DeepEquals(Docx.Read(expected.Document), Docx.Read(actual)));
        var text = Docx.Text(Docx.Read(actual));
        Assert.Contains("Northwind", text);
        Assert.Contains("Riverside", text);
        Assert.Contains("Сто двадцать пять тысяч", text);
        Assert.Equal(before, File.ReadAllBytes(template));
    }

    [Theory]
    [InlineData(true, "stdout")]
    [InlineData(false, "stdout")]
    [InlineData(true, "new")]
    [InlineData(false, "new")]
    [InlineData(true, "existing")]
    [InlineData(false, "existing")]
    public async Task Real_entry_point_rejects_invalid_templates_without_partial_or_overwritten_outputs(bool nested, string mode)
    {
        var directory = EvidenceDirectory();
        var template = nested ? Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))) : Docx.Create(Docx.ConflictingLayout("paragraph"));
        Docx.AssertValid(template);
        var path = Path.Combine(directory, "invalid.docx");
        var output = Path.Combine(directory, "result.docx");
        File.WriteAllBytes(path, template);
        var sentinel = Encoding.UTF8.GetBytes("existing output must survive");
        if (mode == "existing") File.WriteAllBytes(output, sentinel);
        var arguments = new List<string> { "--template", path, "--json", "{\"Name\":\"value\",\"A\":[{}],\"B\":[{}]}" };
        if (mode != "stdout") arguments.AddRange(["--output", output]);
        var result = await Run(directory, arguments);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("/word/document.xml", result.Error);
        Assert.Contains("paragraph 1", result.Error);
        Assert.Contains(nested ? "plain cached text" : "separate supported", result.Error);
        Assert.DoesNotContain("Stack Trace", result.Error);
        Assert.Equal(template, File.ReadAllBytes(path));
        if (mode == "existing") Assert.Equal(sentinel, File.ReadAllBytes(output));
        else Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Real_entry_point_preserves_help_argument_and_json_error_behavior()
    {
        var directory = EvidenceDirectory();
        var help = await Run(directory, ["--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("MonoReport.Cli", Encoding.UTF8.GetString(help.Output));
        Assert.Empty(help.Error);
        var arguments = await Run(directory, ["--unknown"]);
        Assert.Equal(2, arguments.ExitCode);
        Assert.Empty(arguments.Output);
        Assert.NotEmpty(arguments.Error);
        var invalidJson = await Run(directory, ["--template", Path.Combine(AppContext.BaseDirectory, "sample.template.docx"), "--json", "invalid-json"]);
        Assert.Equal(1, invalidJson.ExitCode);
        Assert.Empty(invalidJson.Output);
        Assert.NotEmpty(invalidJson.Error);
    }

    [Theory]
    [InlineData("valid", 0, true)]
    [InlineData("structural", 1, false)]
    [InlineData("corrupt", 1, false)]
    [InlineData("empty", 1, false)]
    public async Task Real_entry_point_validation_returns_exact_json_and_exit_contract(
        string fixture, int expectedExit, bool expectedValid)
    {
        var directory = EvidenceDirectory();
        var path = Path.Combine(directory, fixture + ".docx");
        var bytes = fixture switch
        {
            "valid" => Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing \\* UnavailableFormatter"))),
            "structural" => Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))),
            "corrupt" => Encoding.UTF8.GetBytes("not a DOCX package"),
            _ => []
        };
        File.WriteAllBytes(path, bytes);

        var result = await Run(directory, ["--validate", "--template", path]);

        Assert.Equal(expectedExit, result.ExitCode);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
        Assert.Equal(expectedValid, json.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("errors").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("warnings").ValueKind);
        Assert.Equal(expectedValid ? 0 : 1, json.RootElement.GetProperty("errors").GetArrayLength());
    }

    public static IEnumerable<object[]> InvalidValidationArguments()
    {
        yield return new object[] { new[] { "--validate" } };
        yield return new object[] { new[] { "--validate", "--template", "" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "--json", "{}" } };
        yield return new object[] { new[] { "--json-file", "data.json", "--validate", "--template", "a" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "--stdin" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "--culture", "en-US" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "--output", "-" } };
        yield return new object[] { new[] { "-o", "result.docx", "--validate", "-t", "a" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "--unknown" } };
        yield return new object[] { new[] { "--validate", "--template", "a", "positional" } };
        yield return new object[] { new[] { "--validate", "--validate", "--template", "a" } };
        yield return new object[] { new[] { "--validate", "-t", "a", "--template", "b" } };
    }

    [Theory]
    [MemberData(nameof(InvalidValidationArguments))]
    public async Task Real_entry_point_validation_rejects_every_invalid_argument_class(string[] arguments)
    {
        var result = await Run(EvidenceDirectory(), arguments);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task Real_entry_point_validation_handles_help_and_missing_file()
    {
        var directory = EvidenceDirectory();
        var help = await Run(directory, ["--validate", "--unknown", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("--validate", Encoding.UTF8.GetString(help.Output));
        Assert.Empty(help.Error);

        var missing = await Run(directory, ["--validate", "--template", Path.Combine(directory, "missing.docx")]);
        Assert.Equal(1, missing.ExitCode);
        Assert.Empty(missing.Output);
        Assert.Contains("could not be completed", missing.Error);
        Assert.DoesNotContain(directory, missing.Error);
    }

    [Fact]
    public async Task Real_entry_point_validation_exits_while_redirected_stdin_remains_open()
    {
        var directory = EvidenceDirectory();
        var template = Path.Combine(AppContext.BaseDirectory, "sample.template.docx");
        var result = await Run(directory, ["--validate", "--template", template], keepInputOpen: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
    }

    private static string EvidenceDirectory()
    {
        var root = Environment.GetEnvironmentVariable("MONOREPORT_TEST_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "feature002-di-evidence");
        var directory = Path.Combine(root, "cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<ProcessResult> Run(string evidence, IReadOnlyList<string> arguments,
        string? input = null, bool keepInputOpen = false)
    {
        // Use the executable project output with its own runtimeconfig and deps files.
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var repository = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Parent!.Parent!.Parent!.FullName;
        var cli = Path.Combine(repository, "MonoReport.Cli", "bin", configuration, "net10.0", "MonoReport.Cli.dll");
        Assert.True(File.Exists(Path.ChangeExtension(cli, ".runtimeconfig.json")), cli);
        Assert.True(File.Exists(Path.ChangeExtension(cli, ".deps.json")), cli);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(cli);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        Assert.True(process.Start());
        using var output = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (input is not null) await process.StandardInput.WriteAsync(input);
            if (!keepInputOpen) process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (keepInputOpen) process.StandardInput.Close();
            await outputTask.WaitAsync(TimeSpan.FromSeconds(5));
            var error = await errorTask.WaitAsync(TimeSpan.FromSeconds(5));
            var result = new ProcessResult(process.ExitCode, output.ToArray(), error);
            var id = Guid.NewGuid().ToString("N");
            File.WriteAllBytes(Path.Combine(evidence, id + ".stdout"), result.Output);
            File.WriteAllText(Path.Combine(evidence, id + ".stderr"), result.Error);
            File.WriteAllText(Path.Combine(evidence, id + ".json"), System.Text.Json.JsonSerializer.Serialize(new { executable = "dotnet", arguments = new[] { cli }.Concat(arguments), result.ExitCode, stdoutBytes = result.Output.Length, stderr = result.Error }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return result;
        }
        finally
        {
            process.StandardInput.Dispose();
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    private sealed record ProcessResult(int ExitCode, byte[] Output, string Error);
}
