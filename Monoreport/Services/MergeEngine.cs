using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class MergeEngine(DocumentRenderer renderer)
    {
        private readonly DocumentRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

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
                using (var document = WordprocessingDocument.Open(output, true))
                {
                    var main = document.MainDocumentPart ?? throw new TemplateException("DOCX has no main document part.");
                    foreach (var part in StoryParts(main))
                    {
                        XDocument xml;
                        using (var input = part.GetStream(FileMode.Open, FileAccess.Read))
                        {
                            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                            {
                                xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                            }
                        }

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
