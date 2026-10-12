namespace MonoReport.Models;

internal sealed record FieldInspection(
    IReadOnlyList<FieldReference> Fields,
    IReadOnlyList<TemplateException> Errors,
    bool StructureIsSafe);
