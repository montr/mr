using System.Xml.Linq;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class TemplateFormatterTests
    {
        private const string AmountCode = " MERGEFIELD Totals.Amount ";
        private const string SpellOutCode = " MERGEFIELD Totals.Amount \\* SpellOut \\* MERGEFORMAT ";
        private const string AmountJson = "{\"Totals\":{\"Amount\":125430.50}}";
        private const string ExpectedWords = "Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SpellOut_switch_formats_only_its_field_and_survives_remerge(bool complex)
        {
            var paragraph = complex
                ? Docx.P(
                    new XElement(WordXml.RunElementName, new XElement(WordXml.FieldCharacterElementName,
                        new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldBeginValue))),
                    new XElement(WordXml.RunElementName, new XElement(WordXml.InstructionTextElementName, SpellOutCode[..20])),
                    new XElement(WordXml.RunElementName, new XElement(WordXml.InstructionTextElementName, SpellOutCode[20..])),
                    new XElement(WordXml.RunElementName, new XElement(WordXml.FieldCharacterElementName,
                        new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldSeparatorValue))),
                    new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, "old")),
                    new XElement(WordXml.RunElementName, new XElement(WordXml.FieldCharacterElementName,
                        new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldEndValue))))
                : Docx.P(Docx.Field(SpellOutCode, properties: Docx.Properties()));
            var template = Docx.Create(Docx.P(Docx.Field(AmountCode)), paragraph);
            var first = Docx.Merge(template, AmountJson);
            var xml = Docx.Read(first.Document);
            Assert.Equal("125430.50" + ExpectedWords, Docx.Text(xml));
            Assert.True(XNode.DeepEquals(Docx.Properties(), Assert.Single(xml.Descendants(WordXml.RunPropertiesElementName))));
            var second = Docx.Merge(first.Document, "{\"Totals\":{\"Amount\":2.02}}");
            var secondXml = Docx.Read(second.Document);
            Assert.Equal("2.02Два рубля 02 копейки", Docx.Text(secondXml));
            if (complex)
            {
                Assert.Equal(SpellOutCode, string.Concat(secondXml.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value)));
            }
            else
            {
                Assert.Equal(SpellOutCode, (string?)secondXml.Descendants(WordXml.SimpleFieldElementName).Last().Attribute(WordXml.InstructionAttributeName));
            }
        }

        [Theory]
        [InlineData("spellout")]
        [InlineData("SpellOut")]
        [InlineData("SPELLOUT")]
        public void SpellOut_is_case_insensitive_and_combines_with_upper(string formatter)
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(AmountCode + "\\* " + formatter + " \\* Upper"))), AmountJson);
            Assert.Equal(ExpectedWords.ToUpperInvariant(), Docx.Text(Docx.Read(result.Document)));
        }

        [Fact]
        public void Unknown_switch_reports_field_and_formatter()
        {
            var code = AmountCode + "\\* MissingFormatter";
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), AmountJson));
            Assert.Equal("Totals.Amount", error.Field);
            Assert.Equal(code, error.FieldCode);
            Assert.Contains("MissingFormatter", error.Formatter);
        }

        [Fact]
        public void SpellOut_works_in_repeated_rows()
        {
            var template = Docx.Create(Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:Items")),
                Docx.Row(Docx.Field("MERGEFIELD Amount \\* SpellOut")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Items"))));
            var result = Docx.Merge(template, "{\"Items\":[{\"Amount\":1},{\"Amount\":2}]}");
            Assert.Equal("Один рубль 00 копеекДва рубля 00 копеек", Docx.Text(Docx.Read(result.Document)));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"Totals\":{\"Amount\":null}}")]
        public void Missing_amount_with_SpellOut_is_empty(string json)
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(SpellOutCode))), json);
            Assert.Equal(string.Empty, Docx.Text(Docx.Read(result.Document)));
        }
    }
}
