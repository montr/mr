using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services
{
    internal static class Docx
    {
        internal static byte[] CreateStories(string? invalidPart, params XElement[] invalid)
        {
            var w = WordXml.DocumentElementName.Namespace;
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XElement[] Content(string uri) => invalidPart == uri ? invalid.Select(x => new XElement(x)).ToArray() : new[]
            {
                P(Field("MERGEFIELD Name \\* SpellOut")), P(Field("PAGE", "7")),
                P(Field("MERGEFIELD TableStart:Empty")), P(Field("MERGEFIELD TableEnd:Empty")),
                P(Field("MERGEFIELD TableStart:Items")), P(Field("MERGEFIELD Item", properties: Properties())), P(Field("MERGEFIELD TableEnd:Items"))
            };
            using var stream = new MemoryStream();
            stream.Write(Create(Content("/word/document.xml")));
            stream.Position = 0;
            using (var document = WordprocessingDocument.Open(stream, true))
            {
                var main = document.MainDocumentPart!;
                var header = main.AddNewPart<HeaderPart>();
                var footer = main.AddNewPart<FooterPart>();
                var footnotes = main.AddNewPart<FootnotesPart>();
                var endnotes = main.AddNewPart<EndnotesPart>();
                void Save(OpenXmlPart part, XElement xml)
                {
                    using var target = part.GetStream(FileMode.Create, FileAccess.Write);
                    new XDocument(xml).Save(target);
                }
                Save(header, new XElement(w + "hdr", Content(header.Uri.ToString())));
                Save(footer, new XElement(w + "ftr", Content(footer.Uri.ToString())));
                Save(footnotes, new XElement(w + "footnotes", new XElement(w + "footnote", new XAttribute(w + "id", "1"), Content(footnotes.Uri.ToString()))));
                Save(endnotes, new XElement(w + "endnotes", new XElement(w + "endnote", new XAttribute(w + "id", "1"), Content(endnotes.Uri.ToString()))));
                var mainContents = Content("/word/document.xml").ToList();
                mainContents.Add(P(new XElement(w + "r", new XElement(w + "footnoteReference", new XAttribute(w + "id", "1")), new XElement(w + "endnoteReference", new XAttribute(w + "id", "1")))));
                mainContents.Add(new XElement(w + "sectPr", new XElement(w + "headerReference", new XAttribute(w + "type", "default"), new XAttribute(r + "id", main.GetIdOfPart(header))), new XElement(w + "footerReference", new XAttribute(w + "type", "default"), new XAttribute(r + "id", main.GetIdOfPart(footer)))));
                Save(main, new XElement(w + "document", new XElement(w + "body", mainContents)));
            }
            return stream.ToArray();
        }

        internal static IReadOnlyDictionary<string, XDocument> ReadStories(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(stream, false);
            var main = doc.MainDocumentPart!;
            var parts = new List<OpenXmlPart> { main };
            parts.AddRange(main.HeaderParts);
            parts.AddRange(main.FooterParts);
            if (main.FootnotesPart is not null) parts.Add(main.FootnotesPart);
            if (main.EndnotesPart is not null) parts.Add(main.EndnotesPart);
            return parts.ToDictionary(p => p.Uri.ToString(), p => { using var input = p.GetStream(); return XDocument.Load(input); });
        }

        internal static XElement Character(string type) => new(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, type));

        internal static XElement[] ComplexContent(string code, params XElement[] result) => new[]
        {
            new XElement(WordXml.RunElementName, Character("begin"), new XElement(WordXml.InstructionTextElementName, code), Character("separate"))
        }.Concat(result).Append(new XElement(WordXml.RunElementName, Character("end"))).ToArray();

        internal static XElement[] NestedResult(bool outerSimple, bool innerSimple, string outerCode, string innerCode, bool noInnerResult = false)
        {
            var inner = innerSimple ? new[] { Field(innerCode, noInnerResult ? "" : "cache") }
                : noInnerResult ? new[] { new XElement(WordXml.RunElementName, Character("begin"), new XElement(WordXml.InstructionTextElementName, innerCode), Character("end")) }
                : ComplexContent(innerCode, Run("cache"));
            return outerSimple ? new[] { new XElement(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, outerCode), inner) } : ComplexContent(outerCode, inner);
        }

        internal static XElement Run(string text) => new(WordXml.RunElementName, new XElement(WordXml.TextElementName, text));

        internal static XElement P(params object[] contents) => new(WordXml.ParagraphElementName, contents);

        internal static XElement Field(string code, string cached = "old", XElement? properties = null) => new(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, code), new XElement(WordXml.RunElementName, properties, new XElement(WordXml.TextElementName, cached)));

        internal static XElement Row(params XElement[] contents) => new(WordXml.TableRowElementName, contents.Select(x => new XElement(WordXml.TableCellElementName, P(x))));

        internal static XElement Table(params XElement[] rows) => new(WordXml.TableElementName, new XElement(WordXml.TablePropertiesElementName), new XElement(WordXml.TableGridElementName, Enumerable.Range(0, 3).Select(_ => new XElement(WordXml.GridColumnElementName, new XAttribute(WordXml.WidthAttributeName, "2000")))), rows);

        internal static XElement[] ConflictingLayout(string layout, string secondName = "B")
        {
            var startA = Field("MERGEFIELD TableStart:A");
            var endA = Field("MERGEFIELD TableEnd:A");
            var startB = Field("MERGEFIELD TableStart:" + secondName);
            var endB = Field("MERGEFIELD TableEnd:" + secondName);
            return layout switch
            {
                "paragraph" => [P(startA, endA, startB, endB)],
                "row" => [Table(Row(startA, endA, startB, endB))],
                "boundary-paragraph" => [P(startA), P(endA, startB), P(endB)],
                "boundary-row" => [Table(Row(startA), Row(endA, startB), Row(endB))],
                "ancestor-paragraph" => [Table(new XElement(WordXml.TableRowElementName, new XElement(WordXml.TableCellElementName, P(startA, endA), P(startB, endB))))],
                "ancestor-table" => [Table(new XElement(WordXml.TableRowElementName, new XElement(WordXml.TableCellElementName, P(startA, endA), Table(Row(startB, endB)), P())))],
                _ => throw new ArgumentException(layout)
            };
        }

        internal static XElement Properties() => new(WordXml.RunPropertiesElementName, new XElement(WordXml.RunFontsElementName, new XAttribute(WordXml.AsciiFontAttributeName, "Arial"), new XAttribute(WordXml.HighAnsiFontAttributeName, "Arial")), new XElement(WordXml.BoldElementName), new XElement(WordXml.ItalicElementName), new XElement(WordXml.ColorElementName, new XAttribute(WordXml.ValueAttributeName, "FF0000")), new XElement(WordXml.FontSizeElementName, new XAttribute(WordXml.ValueAttributeName, "28")), new XElement(WordXml.HighlightElementName, new XAttribute(WordXml.ValueAttributeName, "yellow")), new XElement(WordXml.UnderlineElementName, new XAttribute(WordXml.ValueAttributeName, WordXml.SingleUnderlineValue)), new XElement(WordXml.LanguageElementName, new XAttribute(WordXml.ValueAttributeName, "ru-RU")));

        internal static byte[] Create(params XElement[] content)
        {
            using (var stream = new MemoryStream())
            {
                using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    var main = doc.AddMainDocumentPart();
                    using (var target = main.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        new XDocument(new XElement(WordXml.DocumentElementName, new XAttribute(WordXml.WordprocessingNamespaceDeclarationName, WordXml.WordprocessingNamespace), new XElement(WordXml.BodyElementName, content))).Save(target);
                    }
                }

                return stream.ToArray();
            }
        }

        internal static MergeEngine CreateEngine(FormatterRegistry? formatters = null)
        {
            var registry = formatters ?? new FormatterRegistry(new Dictionary<string, IValueFormatter>
            {
                [RubSpellOutFormatter.FormatterName] = new RubSpellOutFormatter()
            });
            var fieldParser = new OoxmlFieldParser(new FieldCodeParser());
            var renderer = new DocumentRenderer(new DataResolver(), fieldParser, new RegionParser(),
                new ValueFormatter(registry), new CachedResultWriter());
            var storyReader = new DocxStoryReader();
            return new MergeEngine(renderer, storyReader,
                new TemplateAnalyzer(storyReader, new TemplateStructureValidator(fieldParser, new RegionParser())));
        }

        internal static MergeResult Merge(byte[] template, string json, MergeOptions? options = null, FormatterRegistry? formatters = null)
        {
            using (var stream = new MemoryStream(template))
            {
                using (var data = JsonDocument.Parse(json))
                {
                    var result = CreateEngine(formatters).Merge(stream, data, options);
                    Assert.True(stream.CanRead);
                    Assert.Equal(template, stream.ToArray());
                    AssertValid(result.Document);
                    return result;
                }
            }
        }

        internal static XDocument Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var doc = WordprocessingDocument.Open(stream, false))
                {
                    using (var part = doc.MainDocumentPart!.GetStream())
                    {
                        return XDocument.Load(part);
                    }
                }
            }
        }

        internal static string Text(XContainer xml) => string.Concat(xml.Descendants(WordXml.TextElementName).Select(x => x.Value));

        internal static void AssertValid(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var doc = WordprocessingDocument.Open(stream, false))
                {
                    var errors = new OpenXmlValidator().Validate(doc).Select(x => x.Description + " " + x.Path?.XPath).ToArray();
                    Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
                }
            }
        }
    }
}
