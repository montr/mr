using DocumentFormat.OpenXml.Packaging;
using MonoReport.Models;

namespace MonoReport.Services;

public sealed class TemplateValidator(DocxStoryReader storyReader, TemplateStructureValidator structureValidator)
{
    private readonly DocxStoryReader _storyReader = storyReader ?? throw new ArgumentNullException(nameof(storyReader));
    private readonly TemplateStructureValidator _structureValidator = structureValidator ?? throw new ArgumentNullException(nameof(structureValidator));

    public TemplateValidationResult Validate(Stream template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!template.CanRead) throw new ArgumentException("Template stream must be readable.", nameof(template));
        using var buffer = new MemoryStream();
        template.CopyTo(buffer);
        buffer.Position = 0;
        var document = _storyReader.OpenForValidation(buffer, false, out var packageDiagnostic);
        if (packageDiagnostic is not null)
            return new([_structureValidator.Issue(packageDiagnostic)]);
        using (document)
        {
            var inspection = _storyReader.Inspect(document!);
            var errors = inspection.Diagnostics.Select(diagnostic =>
                _structureValidator.Issue(diagnostic, diagnostic.Location)).ToList();
            foreach (var story in inspection.Stories)
                errors.AddRange(_structureValidator.Validate(story.Xml.Root!, story.Part.Uri.ToString()));
            errors = errors.Select((error, index) => (error, index))
                .OrderBy(item => PartOrder(item.error.Part))
                .ThenBy(item => item.index)
                .Select(item => item.error)
                .ToList();
            return new TemplateValidationResult(errors);
        }
    }

    private static (int Kind, string Part) PartOrder(string? part) => part switch
    {
        "/word/document.xml" => (0, part),
        not null when part.StartsWith("/word/header", StringComparison.Ordinal) => (1, part),
        not null when part.StartsWith("/word/footer", StringComparison.Ordinal) => (2, part),
        "/word/footnotes.xml" => (3, part),
        "/word/endnotes.xml" => (4, part),
        _ => (5, part ?? string.Empty)
    };
}
