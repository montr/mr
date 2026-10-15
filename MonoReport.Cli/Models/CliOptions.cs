using System.Globalization;

namespace MonoReport.Cli.Models
{
    public sealed record CliOptions(string TemplatePath, string? OutputPath, string? Json,
        string? JsonPath, CultureInfo Culture, bool Help, bool Analyze = false);
}
