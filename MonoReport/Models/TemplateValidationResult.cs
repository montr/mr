namespace MonoReport.Models;

public sealed class TemplateValidationResult(IEnumerable<TemplateValidationIssue> errors,
    IEnumerable<TemplateValidationIssue>? warnings = null)
{
    public IReadOnlyList<TemplateValidationIssue> Errors { get; } =
        Array.AsReadOnly((errors ?? throw new ArgumentNullException(nameof(errors))).ToArray());
    public IReadOnlyList<TemplateValidationIssue> Warnings { get; } =
        Array.AsReadOnly(warnings?.ToArray() ?? []);
    public bool Valid => Errors.Count == 0;
}
