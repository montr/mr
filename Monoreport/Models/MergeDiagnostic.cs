namespace Monoreport.Models
{
    public sealed record MergeDiagnostic(string Message, string Field, string? FieldCode, string? Location);
}
