using System.Globalization;
using Monoreport.Cli.Models;

namespace Monoreport.Cli.Services
{
    public sealed class CliOptionsParser
    {
        public CliOptions Parse(string[] args)
        {
            string? template = null;
            string? output = null;
            string? json = null;
            string? jsonFile = null;
            var culture = CultureInfo.InvariantCulture;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sources = 0;
            if (args.Contains(CliArguments.Help) || args.Contains(CliArguments.HelpShort))
            {
                return new(string.Empty, null, null, null, culture, true);
            }

            for (var index = 0; index < args.Length; index++)
            {
                var option = args[index] switch
                {
                    CliArguments.TemplateShort => CliArguments.Template,
                    CliArguments.OutputShort => CliArguments.Output,
                    _ => args[index]
                };
                if (seen.Add(option) == false)
                {
                    throw new ArgumentException($"Parameter specified more than once: {option}");
                }

                if (option == CliArguments.StandardInput)
                {
                    sources++;
                    continue;
                }

                if (option is not (CliArguments.Template or CliArguments.Output or CliArguments.Json
                    or CliArguments.JsonFile or CliArguments.Culture))
                {
                    throw new ArgumentException($"Unknown parameter: {option}");
                }

                if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new ArgumentException($"Parameter requires a value: {option}");
                }

                var value = args[index];
                switch (option)
                {
                    case CliArguments.Template:
                        template = value;
                        break;
                    case CliArguments.Output:
                        output = value == CliArguments.StandardStream ? null : value;
                        break;
                    case CliArguments.Json:
                        json = value;
                        sources++;
                        break;
                    case CliArguments.JsonFile:
                        jsonFile = value;
                        sources++;
                        break;
                    case CliArguments.Culture:
                        culture = CultureInfo.GetCultureInfo(value);
                        break;
                }
            }

            if (template is null)
            {
                throw new ArgumentException($"Required parameter: {CliArguments.Template}");
            }

            if (sources > 1)
            {
                throw new ArgumentException("Specify only one JSON source: --json, --json-file or --stdin.");
            }

            return new(template, output, json, jsonFile, culture, false);
        }
    }
}
