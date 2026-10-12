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
        var errors = new List<TemplateValidationIssue>();
        try
        {
            using var document = _storyReader.Open(buffer, false);
            var stories = _storyReader.Read(document, errors);
            foreach (var story in stories)
                errors.AddRange(_structureValidator.Validate(story.Xml.Root!, story.Part.Uri.ToString()));
        }
        catch (TemplateException error)
        {
            errors.Add(_structureValidator.Issue(error));
        }
        return new TemplateValidationResult(errors);
    }
}
