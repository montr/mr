using Monoreport.Cli.Services;
using Monoreport.Services;

namespace Monoreport.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter>
            {
                [RubSpellOutFormatter.FormatterName] = new RubSpellOutFormatter()
            });
            var fieldParser = new OoxmlFieldParser(new FieldCodeParser());
            var renderer = new DocumentRenderer(new DataResolver(), fieldParser, new RegionParser(),
                new ValueFormatter(registry), new CachedResultWriter());
            var application = new CliApplication(new CliOptionsParser(), new MergeEngine(renderer));
            return application.Run(args, Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.Error);
        }
    }
}
