using System.Globalization;

namespace Monoreport.Models
{
    public sealed class MergeOptions
    {
        public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

        public MissingValueBehavior MissingValues { get; init; } = MissingValueBehavior.EmptyString;

        // Optional programmatic formatter bindings; templates can use the SpellOut field switch.
        public IReadOnlyDictionary<string, FormatterBinding> FieldFormatters { get; init; } = new Dictionary<string, FormatterBinding>();
    }
}
