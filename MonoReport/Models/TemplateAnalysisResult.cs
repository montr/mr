namespace MonoReport.Models;

public sealed class TemplateAnalysisResult(IEnumerable<TemplateValidationIssue> errors,
    IEnumerable<TemplateValidationIssue>? warnings = null, IEnumerable<TemplateField>? fields = null)
{
    public IReadOnlyList<TemplateValidationIssue> Errors { get; } =
        Array.AsReadOnly((errors ?? throw new ArgumentNullException(nameof(errors))).ToArray());
    public IReadOnlyList<TemplateValidationIssue> Warnings { get; } =
        Array.AsReadOnly(warnings?.ToArray() ?? []);
    public IReadOnlyList<TemplateField> Fields { get; } =
        Array.AsReadOnly(fields?.ToArray() ?? []);
    public bool Valid => Errors.Count == 0;
}
