namespace MonoReport.Models;

internal sealed record TemplateInspection(IReadOnlyList<AnalyzedStory> Stories,
    IReadOnlyList<StructuralDiagnostic> Diagnostics, IReadOnlyList<TemplateValidationIssue> Warnings,
    IReadOnlyList<TemplateField> Fields);
