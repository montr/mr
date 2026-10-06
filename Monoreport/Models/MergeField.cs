using Monoreport.Services;

namespace Monoreport.Models
{
    public sealed record MergeField(string Name, string Code, IReadOnlyList<FieldSwitch> Switches)
    {
        public bool IsStart => Name.StartsWith(WordFieldCodes.RegionStartPrefix, StringComparison.OrdinalIgnoreCase);

        public bool IsEnd => Name.StartsWith(WordFieldCodes.RegionEndPrefix, StringComparison.OrdinalIgnoreCase);

        public bool IsRegion => IsStart || IsEnd;

        public string Collection => Name[(Name.IndexOf(':') + 1)..];
    }
}
