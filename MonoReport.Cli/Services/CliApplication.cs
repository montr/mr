using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using MonoReport.Cli.Models;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Cli.Services
{
    public sealed class CliApplication(CliOptionsParser parser, MergeEngine engine, TemplateValidator validator)
    {
        private const string HelpText = """
            MonoReport.Cli --template <template.docx> [--output <result.docx>]
                   [--json <JSON> | --json-file <data.json> | --stdin]
                   [--culture <culture>]
            MonoReport.Cli --validate --template <template.docx>

            -t, --template   Required DOCX template path.
            -o, --output     Output DOCX path. Omit or use '-' for binary stdout.
            --json           Inline JSON (quote it according to your shell).
            --json-file      UTF-8 JSON file path.
            --stdin          Read UTF-8 JSON from stdin (default when no source is specified).
            --culture        Formatting culture, e.g. ru-RU. Default: invariant culture.
            --validate       Validate template structure only; accepts only --template.
            -h, --help       Show this help.

            Exit codes: 0 success, 1 input/render/output error, 2 invalid arguments.
            Errors are written to stderr; DOCX output on stdout is binary.
            Validation writes JSON to stdout: 0 valid, 1 invalid/execution failure, 2 invalid arguments.
            """;

        private readonly CliOptionsParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        private readonly MergeEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        private readonly TemplateValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));

        public int Run(string[] args, Stream standardInput, Stream standardOutput, TextWriter diagnostics)
        {
            CliOptions options;

            try
            {
                options = _parser.Parse(args);
            }
            catch (ArgumentException error)
            {
                diagnostics.WriteLine(error.Message);
                return 2;
            }

            try
            {
                if (options.Help)
                {
                    using (var writer = new StreamWriter(standardOutput, new UTF8Encoding(false), leaveOpen: true))
                    {
                        writer.WriteLine(HelpText);
                    }

                    return 0;
                }

                if (options.Validate)
                {
                    return Validate(options, standardOutput, diagnostics);
                }

                var json = ReadJson(options, standardInput);
                MergeResult result;
                using (var data = JsonDocument.Parse(json))
                {
                    using (var template = File.OpenRead(options.TemplatePath))
                    {
                        result = _engine.Merge(template, data, new MergeOptions
                        {
                            Culture = options.Culture
                        });
                    }
                }

                if (options.OutputPath is null)
                {
                    standardOutput.Write(result.Document);
                    standardOutput.Flush();
                }
                else
                {
                    File.WriteAllBytes(options.OutputPath, result.Document);
                }

                return 0;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
                or TemplateException or OpenXmlPackageException or ArgumentException or InvalidOperationException)
            {
                diagnostics.WriteLine(error.Message);
                return 1;
            }
        }

        private int Validate(CliOptions options, Stream standardOutput, TextWriter diagnostics)
        {
            try
            {
                TemplateValidationResult result;
                using (var template = File.OpenRead(options.TemplatePath))
                {
                    result = _validator.Validate(template);
                }

                var bytes = JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                standardOutput.Write(bytes);
                standardOutput.Flush();
                return result.Valid ? 0 : 1;
            }
            catch (Exception)
            {
                diagnostics.WriteLine("Template validation could not be completed.");
                return 1;
            }
        }

        private static string ReadJson(CliOptions options, Stream standardInput)
        {
            if (options.Json is not null)
            {
                return options.Json;
            }

            if (options.JsonPath is not null)
            {
                return File.ReadAllText(options.JsonPath, Encoding.UTF8);
            }

            using (var reader = new StreamReader(standardInput, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
