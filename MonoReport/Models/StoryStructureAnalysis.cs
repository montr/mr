namespace MonoReport.Models;

internal sealed record StoryStructureAnalysis(IReadOnlyList<FieldReference> Fields,
    IReadOnlyList<Region> Regions, IReadOnlyList<StructuralDiagnostic> Diagnostics, bool HierarchyIsSafe);
