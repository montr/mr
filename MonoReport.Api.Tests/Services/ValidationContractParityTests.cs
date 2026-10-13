using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MonoReport.Tests.Services;

public sealed class ValidationContractParityTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("corrupt")]
    [InlineData("stories")]
    public async Task Actual_cli_and_http_return_identical_complete_diagnostics(string kind)
    {
        var bytes = kind switch
        {
            "corrupt" => new byte[] { 1, 2, 3 },
            "invalid" => Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableEnd:Items"))),
            "stories" => HttpApiTests.MultiStoryErrors(),
            _ => Docx.Create(Docx.P(Docx.Field("MERGEFIELD Missing \\* Unknown")))
        };
        var path = Path.Combine(Path.GetTempPath(), "feature003-parity-" + Guid.NewGuid().ToString("N") + ".docx");
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Parent!.Parent!.Parent!.FullName;
            var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            foreach (var argument in new[] { Path.Combine(root, "MonoReport.Cli", "bin", configuration, "net10.0", "MonoReport.Cli.dll"), "--validate", "--template", path }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
            Assert.Empty(await stderr);
            Assert.Equal(kind == "valid" ? 0 : 1, process.ExitCode);
            using var cli = JsonDocument.Parse(await stdout);
            using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient();
            using var form = HttpApiTests.Form(bytes); using var response = await client.PostAsync("/api/documents/validate", form);
            Assert.Equal(200, (int)response.StatusCode);
            using var http = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(JsonElement.DeepEquals(cli.RootElement, http.RootElement));
            foreach (var issue in http.RootElement.GetProperty("errors").EnumerateArray())
                Assert.Equal(new[] { "code", "field", "location", "message", "part" }, issue.EnumerateObject().Select(x => x.Name).Order().ToArray());
        }
        finally { File.Delete(path); }
    }
}
