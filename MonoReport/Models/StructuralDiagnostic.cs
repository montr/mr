namespace MonoReport.Models;

internal sealed record StructuralDiagnostic(
    string Code,
    string Detail,
    string? Field = null,
    string? FieldCode = null,
    string? Location = null,
    string? Formatter = null)
{
    internal TemplateException ToException() =>
        new(Detail, Field, FieldCode, Location, Formatter, code: Code);
}
