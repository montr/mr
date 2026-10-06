using System.Text.Json;

namespace Monoreport.Models
{
    public sealed class MergeContext(JsonElement root, MergeOptions options)
    {
        public Stack<JsonElement> Values { get; } = new([root]);

        public MergeOptions Options { get; } = options;

        public List<MergeDiagnostic> Warnings { get; } = new();
    }
}
