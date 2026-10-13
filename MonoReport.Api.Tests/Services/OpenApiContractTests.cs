using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MonoReport.Tests.Services;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Generated_contract_has_accurate_multipart_binary_json_errors_and_context()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json"); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("security", out _));
        foreach (var name in new[] { "generate", "validate" })
        {
            var operation = root.GetProperty("paths").GetProperty($"/api/documents/{name}").GetProperty("post");
            var schema = operation.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
            Assert.True(operation.GetProperty("requestBody").GetProperty("required").GetBoolean());
            Assert.Equal("binary", schema.GetProperty("properties").GetProperty("template").GetProperty("format").GetString());
            Assert.Equal(name == "generate" ? new[] { "template", "data" } : new[] { "template" }, schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray());
            if (name == "validate") Assert.Single(schema.GetProperty("properties").EnumerateObject());
            else Assert.Equal("string", schema.GetProperty("properties").GetProperty("data").GetProperty("type").GetString());
            var responses = operation.GetProperty("responses");
            Assert.Equal(name == "generate" ? new[] { "200", "400", "413", "415", "422", "500" } : new[] { "200", "400", "413", "415", "500" }, responses.EnumerateObject().Select(x => x.Name).Order().ToArray());
            Assert.Contains("request.missing_input", responses.GetProperty("400").GetProperty("description").GetString());
            var problem = responses.GetProperty("400").GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
            Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
            Assert.True(problem.GetProperty("properties").TryGetProperty("traceId", out _));
            if (name == "generate")
            {
                Assert.Equal("binary", responses.GetProperty("200").GetProperty("content").GetProperty("application/vnd.openxmlformats-officedocument.wordprocessingml.document").GetProperty("schema").GetProperty("format").GetString());
                Assert.Contains("only", responses.GetProperty("200").GetProperty("headers").GetProperty("X-MonoReport-Warning-Count").GetProperty("description").GetString());
            }
            else Assert.True(responses.GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var result = schemas.GetProperty("TemplateValidationResult");
        foreach (var key in new[] { "valid", "errors", "warnings" }) Assert.True(result.GetProperty("properties").TryGetProperty(key, out _));
        Assert.Equal(new[] { "errors", "valid", "warnings" }, result.GetProperty("required").EnumerateArray().Select(x => x.GetString()).Order().ToArray());
        var issue = schemas.GetProperty("TemplateValidationIssue");
        foreach (var key in new[] { "code", "message", "field", "part", "location" }) Assert.True(issue.GetProperty("properties").TryGetProperty(key, out _));
        foreach (var key in new[] { "field", "part", "location" }) Assert.Contains("null", issue.GetProperty("properties").GetProperty(key).GetProperty("type").EnumerateArray().Select(x => x.GetString()));
        using var docs = await client.GetAsync("/docs"); docs.EnsureSuccessStatusCode(); Assert.Contains("swagger", await docs.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Production", null, true)]
    [InlineData("Production", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "false", false)]
    public async Task Documentation_exposure_is_explicit(string environment, string? enabled, bool expected)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["MonoReport:DocumentationEnabled"] = enabled }));
        });
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/openapi/v1.json", "/docs" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(expected ? 200 : 404, (int)response.StatusCode);
        }
    }
}
