namespace Monoreport.Models
{
    public sealed record MergeResult(byte[] Document, IReadOnlyList<MergeDiagnostic> Warnings);
}
