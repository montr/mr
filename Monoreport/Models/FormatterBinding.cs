namespace Monoreport.Models
{
    public sealed record FormatterBinding(string Name, IReadOnlyDictionary<string, string>? Options = null);
}
