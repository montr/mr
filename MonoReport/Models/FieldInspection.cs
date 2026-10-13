namespace MonoReport.Models;

internal sealed record FieldInspection(
    IReadOnlyList<FieldReference> Fields,
    IReadOnlyList<StructuralDiagnostic> Diagnostics,
    bool StructureIsSafe);
