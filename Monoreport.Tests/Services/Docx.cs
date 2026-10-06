using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    internal static class Docx
    {
        internal static XElement Run(string text) => new(WordXml.RunElementName, new XElement(WordXml.TextElementName, text));

        internal static XElement P(params object[] contents) => new(WordXml.ParagraphElementName, contents);

        internal static XElement Field(string code, string cached = "old", XElement? properties = null) => new(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, code), new XElement(WordXml.RunElementName, properties, new XElement(WordXml.TextElementName, cached)));

        internal static XElement Row(params XElement[] contents) => new(WordXml.TableRowElementName, contents.Select(x => new XElement(WordXml.TableCellElementName, P(x))));

        internal static XElement Table(params XElement[] rows) => new(WordXml.TableElementName, new XElement(WordXml.TablePropertiesElementName), new XElement(WordXml.TableGridElementName, Enumerable.Range(0, 3).Select(_ => new XElement(WordXml.GridColumnElementName, new XAttribute(WordXml.WidthAttributeName, "2000")))), rows);

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
            return new MergeEngine(renderer);
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
