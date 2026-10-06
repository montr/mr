using System.Text.Json;
using System.Xml.Linq;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class FieldTests
    {
        [Theory]
        [InlineData("CompanyName", "{\"CompanyName\":\"ООО Ромашка\"}", "ООО Ромашка")]
        [InlineData("Customer.Address.City", "{\"Customer\":{\"Address\":{\"City\":\"Москва\"}}}", "Москва")]
        [InlineData("Missing", "{}", "")]
        [InlineData("Name", "{\"Name\":null}", "")]
        [InlineData("Name", "{\"Name\":true}", "true")]
        [InlineData("Name", "{\"Name\":123.45}", "123.45")]
        public void Resolves_scalars_and_paths(string name, string json, string expected)
        {
            var code = " MERGEFIELD " + name + " ";
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), json);
            var xml = Docx.Read(result.Document);
            Assert.Equal(expected, Docx.Text(xml));
            Assert.Equal(code, (string?)Assert.Single(xml.Descendants(WordXml.SimpleFieldElementName)).Attribute(WordXml.InstructionAttributeName));
        }

        [Theory]
        [InlineData("Upper", "иВан петров", "ИВАН ПЕТРОВ")]
        [InlineData("Lower", "иВан ПЕТРОВ", "иван петров")]
        [InlineData("Caps", "иван петров", "Иван Петров")]
        [InlineData("FirstCap", "иван петров", "Иван петров")]
        [InlineData("FirstCap", "  иван Петров", "  Иван Петров")]
        [InlineData("MERGEFORMAT", "иВан петров", "иВан петров")]
        public void Applies_text_switches(string format, string value, string expected)
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(" MERGEFIELD Name \\* " + format))), JsonSerializer.Serialize(new { Name = value }));
            Assert.Equal(expected, Docx.Text(Docx.Read(result.Document)));
        }

        [Theory]
        [InlineData(" MERGEFIELD Amount \\# \"#,##0.00\" \\* MERGEFORMAT ", "{\"Amount\":12345.6}", "12,345.60")]
        [InlineData(" MERGEFIELD Date \\@ \"dd.MM.yyyy\" ", "{\"Date\":\"2026-09-23\"}", "23.09.2026")]
        [InlineData(" MERGEFIELD \"Company Name\" ", "{\"Company Name\":\"Acme\"}", "Acme")]
        public void Parses_quoted_arguments_and_combined_switches(string code, string json, string expected)
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), json);
            Assert.Equal(expected, Docx.Text(Docx.Read(result.Document)));
        }

        [Fact]
        public void Remerge_preserves_code_and_all_run_properties()
        {
            var code = " MERGEFIELD CompanyName \\* MERGEFORMAT ";
            var properties = Docx.Properties();
            var template = Docx.Create(Docx.P(Docx.Field(code, properties: new(properties))));
            var first = Docx.Merge(template, "{\"CompanyName\":\"ООО Ромашка\"}");
            var second = Docx.Merge(first.Document, "{\"CompanyName\":\"АО Вектор\"}");
            foreach (var bytes in new[]
            {
                first.Document,
                second.Document
            }

            )
            {
                var xml = Docx.Read(bytes);
                Assert.True(XNode.DeepEquals(properties, Assert.Single(xml.Descendants(WordXml.RunPropertiesElementName))));
                Assert.Equal(code, (string?)Assert.Single(xml.Descendants(WordXml.SimpleFieldElementName)).Attribute(WordXml.InstructionAttributeName));
            }

            Assert.Equal("АО Вектор", Docx.Text(Docx.Read(second.Document)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Complex_field_handles_fragmented_code_and_multiple_formatted_result_runs(bool sameRun)
        {
            var begin = new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldBeginValue));
            var code1 = new XElement(WordXml.InstructionTextElementName, " MERGE");
            var code2 = new XElement(WordXml.InstructionTextElementName, "FIELD Name \\* MERGEFORMAT ");
            var separator = new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldSeparatorValue));
            var end = new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldEndValue));
            var p = sameRun ? Docx.P(new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, "before|"), begin, code1, code2, separator, new XElement(WordXml.TextElementName, "old"), end, new XElement(WordXml.TextElementName, "|after"))) : Docx.P(Docx.Run("before|"), new XElement(WordXml.RunElementName, begin, code1), new XElement(WordXml.RunElementName, code2), new XElement(WordXml.RunElementName, separator), new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, "ol")), new XElement(WordXml.RunElementName, new XElement(WordXml.RunPropertiesElementName, new XElement(WordXml.ItalicElementName)), new XElement(WordXml.TextElementName, "d")), new XElement(WordXml.RunElementName, end), Docx.Run("|after"));
            var template = Docx.Create(p);
            var original = Docx.Read(template);
            var first = Docx.Merge(template, "{\"Name\":\"Long replacement\"}");
            var second = Docx.Merge(first.Document, "{\"Name\":\"X\"}");
            var output = Docx.Read(second.Document);
            Assert.Equal("before|Long replacement|after", Docx.Text(Docx.Read(first.Document)));
            Assert.Equal("before|X|after", Docx.Text(output));
            Assert.Equal(original.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value), output.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value));
            Assert.Equal(original.Descendants(WordXml.RunPropertiesElementName).Select(x => x.ToString()), output.Descendants(WordXml.RunPropertiesElementName).Select(x => x.ToString()));
            Assert.Equal(3, output.Descendants(WordXml.FieldCharacterElementName).Count());
        }

        [Fact]
        public void Complex_field_without_cached_result_gets_separator_and_text()
        {
            var template = Docx.Create(Docx.P(new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldBeginValue)), new XElement(WordXml.InstructionTextElementName, " MERGEFIELD Name "), new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldEndValue)))));
            var result = Docx.Merge(template, "{\"Name\":\"Hello\"}");
            Assert.Equal("Hello", Docx.Text(Docx.Read(result.Document)));
            Assert.Equal(3, Docx.Read(result.Document).Descendants(WordXml.FieldCharacterElementName).Count());
        }

        [Fact]
        public void Whitespace_line_breaks_tabs_and_xml_characters_survive()
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))), "{\"Name\":\" A & <B>\\nC\\tD \"}");
            var xml = Docx.Read(result.Document);
            Assert.Single(xml.Descendants(WordXml.BreakElementName));
            Assert.Single(xml.Descendants(WordXml.TabElementName));
            Assert.Equal(" A & <B>CD ", Docx.Text(xml));
            var again = Docx.Merge(result.Document, "{\"Name\":\"ok\"}");
            Assert.Empty(Docx.Read(again.Document).Descendants(WordXml.BreakElementName));
            Assert.Equal("ok", Docx.Text(Docx.Read(again.Document)));
        }

        [Fact]
        public void Missing_values_can_warn_or_throw()
        {
            var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD Customer.Unknown")));
            var result = Docx.Merge(template, "{}", new() { MissingValues = MissingValueBehavior.Warning });
            Assert.Equal("Customer.Unknown", Assert.Single(result.Warnings).Field);
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{}", new() { MissingValues = MissingValueBehavior.Error }));
            Assert.Contains("Unknown template field: Customer.Unknown", error.Message);
            Assert.Equal("/word/document.xml", error.Location);
        }

        [Theory]
        [InlineData("MERGEFIELD Name \\* SPELL_OUT")]
        [InlineData("MERGEFIELD Name \\# \"0.00\"")]
        [InlineData("MERGEFIELD Name \\@ \"dd.MM.yyyy\"")]
        [InlineData("MERGEFIELD Name \\z Nope")]
        public void Formatting_errors_have_field_formatter_and_code(string code)
        {
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(Docx.P(Docx.Field(code))), "{\"Name\":\"not a number or date\"}"));
            Assert.Equal("Name", error.Field);
            Assert.Equal(code, error.FieldCode);
            Assert.False(string.IsNullOrEmpty(error.Formatter));
        }

        [Fact]
        public void Non_merge_fields_are_preserved()
        {
            var field = Docx.Field("PAGE", "3");
            var result = Docx.Merge(Docx.Create(Docx.P(field)), "{}");
            Assert.Equal(field.ToString(), Assert.Single(Docx.Read(result.Document).Descendants(WordXml.SimpleFieldElementName)).ToString());
        }
    }
}
