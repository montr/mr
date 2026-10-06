using System.Text.Json;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class OoxmlEdgeTests
    {
        private static XElement Char(string type) => new(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, type));

        private static XElement Complex(string code) => Docx.P(new XElement(WordXml.RunElementName, Char(WordXml.FieldBeginValue)), new XElement(WordXml.RunElementName, new XElement(WordXml.InstructionTextElementName, code)), new XElement(WordXml.RunElementName, Char(WordXml.FieldSeparatorValue)), Docx.Run("old"), new XElement(WordXml.RunElementName, Char(WordXml.FieldEndValue)));

        [Fact]
        public void Complex_fields_can_span_paragraphs()
        {
            var template = Docx.Create(Docx.P(Docx.Run("prefix"), new XElement(WordXml.RunElementName, Char(WordXml.FieldBeginValue), new XElement(WordXml.InstructionTextElementName, "MERGEFIELD Name"), Char(WordXml.FieldSeparatorValue)), Docx.Run("old")), Docx.P(new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, " cache"), Char(WordXml.FieldEndValue)), Docx.Run("suffix")));
            var result = Docx.Merge(template, "{\"Name\":\"replacement\"}");
            Assert.Equal("prefixreplacementsuffix", Docx.Text(Docx.Read(result.Document)));
            var again = Docx.Merge(result.Document, "{\"Name\":\"new\"}");
            Assert.Equal("prefixnewsuffix", Docx.Text(Docx.Read(again.Document)));
        }

        [Fact]
        public void Complex_region_markers_are_removed()
        {
            var template = Docx.Create(Complex("MERGEFIELD TableStart:Items"), Complex("MERGEFIELD Name"), Complex("MERGEFIELD TableEnd:Items"));
            var result = Docx.Merge(template, "{\"Items\":[{\"Name\":\"A\"},{\"Name\":\"B\"}]}");
            var xml = Docx.Read(result.Document);
            Assert.Equal("AB", Docx.Text(xml));
            Assert.Equal(2, xml.Descendants(WordXml.ParagraphElementName).Count());
            Assert.Equal(6, xml.Descendants(WordXml.FieldCharacterElementName).Count());
            Assert.DoesNotContain("TableStart", xml.ToString());
        }

        [Fact]
        public void Cached_result_inside_hyperlink_preserves_container()
        {
            var p = Docx.P(new XElement(WordXml.RunElementName, Char(WordXml.FieldBeginValue), new XElement(WordXml.InstructionTextElementName, "MERGEFIELD Name"), Char(WordXml.FieldSeparatorValue)), new XElement(WordXml.HyperlinkElementName, new XAttribute(WordXml.AnchorAttributeName, "bookmark"), new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, "old"))), new XElement(WordXml.RunElementName, Char(WordXml.FieldEndValue)));
            var result = Docx.Merge(Docx.Create(p), "{\"Name\":\"value\"}");
            var xml = Docx.Read(result.Document);
            Assert.Equal("value", Docx.Text(Assert.Single(xml.Descendants(WordXml.HyperlinkElementName))));
            Assert.True(XNode.DeepEquals(Docx.Properties(), Assert.Single(xml.Descendants(WordXml.RunPropertiesElementName))));
        }

        [Fact]
        public void Processes_headers_and_footers()
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))));
                stream.Position = 0;
                using (var doc = WordprocessingDocument.Open(stream, true))
                {
                    var main = doc.MainDocumentPart!;
                    var header = main.AddNewPart<HeaderPart>();
                    var footer = main.AddNewPart<FooterPart>();
                    using (var target = header.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        new XDocument(new XElement(WordXml.HeaderElementName, Docx.P(Docx.Field("MERGEFIELD Name")))).Save(target);
                    }

                    using (var target = footer.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        new XDocument(new XElement(WordXml.FooterElementName, Docx.P(Docx.Field("MERGEFIELD Name")))).Save(target);
                    }

                    XDocument xml;
                    using (var source = main.GetStream())
                    {
                        xml = XDocument.Load(source);
                    }

                    xml.Root!.Element(WordXml.BodyElementName)!.Add(new XElement(WordXml.SectionPropertiesElementName, new XElement(WordXml.HeaderReferenceElementName, new XAttribute(WordXml.TypeAttributeName, WordXml.DefaultHeaderFooterTypeValue), new XAttribute(WordXml.RelationshipIdAttributeName, main.GetIdOfPart(header))), new XElement(WordXml.FooterReferenceElementName, new XAttribute(WordXml.TypeAttributeName, WordXml.DefaultHeaderFooterTypeValue), new XAttribute(WordXml.RelationshipIdAttributeName, main.GetIdOfPart(footer)))));
                    using (var output = main.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        xml.Save(output);
                    }
                }

                var result = Docx.Merge(stream.ToArray(), "{\"Name\":\"Value\"}");
                using (var mergedStream = new MemoryStream(result.Document))
                {
                    using (var merged = WordprocessingDocument.Open(mergedStream, false))
                    {
                        foreach (var part in merged.MainDocumentPart!.HeaderParts.Cast<OpenXmlPart>().Concat(merged.MainDocumentPart.FooterParts))
                        {
                            using (var input = part.GetStream())
                            {
                                Assert.Equal("Value", Docx.Text(XDocument.Load(input)));
                            }
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData(WordXml.FieldBeginValue, "Unclosed")]
        [InlineData(WordXml.FieldEndValue, "without begin")]
        [InlineData(WordXml.FieldSeparatorValue, "Unexpected")]
        public void Malformed_complex_fields_are_diagnosed(string type, string message)
        {
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(Docx.P(new XElement(WordXml.RunElementName, Char(type)))), "{}"));
            Assert.Contains(message, error.Message);
            Assert.NotNull(error.Location);
        }

        [Theory]
        [InlineData("MERGEFIELD")]
        [InlineData("MERGEFIELD Name \\*")]
        [InlineData("MERGEFIELD Name \\@ \"unterminated")]
        [InlineData("MERGEFIELD TableStart:")]
        public void Malformed_codes_are_diagnosed(string code)
        {
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), "{}"));
            Assert.Equal(code, error.FieldCode);
            Assert.NotNull(error.Location);
        }

        [Fact]
        public void Empty_region_without_header_is_valid()
        {
            var template = Docx.Create(Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:Items")), Docx.Row(Docx.Field("MERGEFIELD Name")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Items"))));
            var result = Docx.Merge(template, "{\"Items\":[]}");
            Assert.Empty(Docx.Read(result.Document).Descendants(WordXml.TableRowElementName));
        }
    }
}
