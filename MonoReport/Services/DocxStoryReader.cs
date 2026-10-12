using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using MonoReport.Models;

namespace MonoReport.Services;

public sealed class DocxStoryReader
{
    public WordprocessingDocument Open(Stream stream, bool editable)
    {
        try { return WordprocessingDocument.Open(stream, editable); }
        catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or FileFormatException)
        {
            throw new TemplateException("DOCX package is unreadable.", inner: error, code: "template.invalid_package");
        }
    }

    public IReadOnlyList<(OpenXmlPart Part, XDocument Xml)> Read(WordprocessingDocument document,
        List<TemplateValidationIssue>? errors = null)
    {
        var main = document.MainDocumentPart ?? throw new TemplateException("DOCX has no main document part.", code: "template.unsupported_structure");
        var stories = new List<(OpenXmlPart, XDocument)>();
        foreach (var part in Parts(main))
        {
            try
            {
                using var input = part.GetStream(FileMode.Open, FileAccess.Read);
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
                var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                var expected = part switch
                {
                    MainDocumentPart => "document", HeaderPart => "hdr", FooterPart => "ftr",
                    FootnotesPart => "footnotes", EndnotesPart => "endnotes", _ => ""
                };
                if (xml.Root?.Name != WordXml.DocumentElementName.Namespace + expected ||
                    (part is MainDocumentPart && xml.Root.Element(WordXml.BodyElementName) is null))
                    throw new TemplateException("Unsupported DOCX story structure.", location: part.Uri.ToString(), code: "template.unsupported_structure");
                stories.Add((part, xml));
            }
            catch (XmlException error)
            {
                var failure = new TemplateException("Invalid DOCX story XML.", location: part.Uri.ToString(), inner: error, code: "template.invalid_xml");
                if (errors is null) throw failure;
                errors.Add(new(failure.Code, "A document part contains invalid XML.", Part: part.Uri.ToString(), Location: part.Uri.ToString()));
            }
            catch (TemplateException error) when (errors is not null)
            {
                errors.Add(new(error.Code, "The DOCX document structure is unsupported.", Part: part.Uri.ToString(), Location: part.Uri.ToString()));
            }
        }
        return stories;
    }

    private static IEnumerable<OpenXmlPart> Parts(MainDocumentPart main)
    {
        yield return main;
        foreach (var part in main.HeaderParts.OrderBy(part => part.Uri.ToString(), StringComparer.Ordinal)) yield return part;
        foreach (var part in main.FooterParts.OrderBy(part => part.Uri.ToString(), StringComparer.Ordinal)) yield return part;
        if (main.FootnotesPart is not null) yield return main.FootnotesPart;
        if (main.EndnotesPart is not null) yield return main.EndnotesPart;
    }
}
