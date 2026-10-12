using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class MergeEngine(DocumentRenderer renderer, DocxStoryReader storyReader,
        TemplateStructureValidator structureValidator)
    {
        private readonly DocumentRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        private readonly DocxStoryReader _storyReader = storyReader ?? throw new ArgumentNullException(nameof(storyReader));
        private readonly TemplateStructureValidator _structureValidator = structureValidator ?? throw new ArgumentNullException(nameof(structureValidator));

        /// <summary>Reads the template from its current position. The caller retains ownership of both inputs.</summary>
        public MergeResult Merge(Stream template, JsonDocument data, MergeOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(data);
            if (template.CanRead == false)
            {
                throw new ArgumentException("Template stream must be readable.", nameof(template));
            }

            options ??= new();
            var context = new MergeContext(data.RootElement, options);
            using (var output = new MemoryStream())
            {
                template.CopyTo(output);
                output.Position = 0;
                using (var document = _storyReader.Open(output, true))
                {
                    var stories = _storyReader.Read(document);
                    foreach (var (part, xml) in stories)
                    {
                        var error = _structureValidator.ValidateErrors(xml.Root!, part.Uri.ToString()).FirstOrDefault();
                        if (error is not null) throw error;
                    }

                    foreach (var (part, xml) in stories)
                    {
                        _renderer.Render(xml.Root!, part.Uri.ToString(), context);
                        using (var target = part.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            xml.Save(target, SaveOptions.DisableFormatting);
                        }
                    }
                }

                return new(output.ToArray(), context.Warnings.AsReadOnly());
            }
        }

        private static IEnumerable<OpenXmlPart> StoryParts(MainDocumentPart main)
        {
            yield return main;
            foreach (var header in main.HeaderParts)
            {
                yield return header;
            }

            foreach (var footer in main.FooterParts)
            {
                yield return footer;
            }

            if (main.FootnotesPart is not null)
            {
                yield return main.FootnotesPart;
            }

            if (main.EndnotesPart is not null)
            {
                yield return main.EndnotesPart;
            }
        }
    }
}
