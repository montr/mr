using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services
{
    public sealed class FieldTests
    {
        public static IEnumerable<object[]> NestedCachedCases()
        {
            foreach (var outer in new[] { true, false })
            foreach (var inner in new[] { true, false })
            foreach (var code in new[] { "PAGE", "MERGEFIELD Inner", "IF 1 = 1 yes no" })
            foreach (var empty in new[] { true, false })
            foreach (var outerCode in new[] { "MERGEFIELD Name", "MERGEFIELD TableStart:Items", "MERGEFIELD TableEnd:Items" })
                yield return new object[] { outer, inner, code, empty, outerCode };
        }

        [Theory]
        [MemberData(nameof(NestedCachedCases))]
        public void Cached_nested_matrix_rejects_all_representations_types_and_markers(bool outer, bool inner, string nestedCode, bool empty, string outerCode)
        {
            var content = Docx.P(Docx.NestedResult(outer, inner, outerCode, nestedCode, empty));
            var template = Docx.Create(content);
            Docx.AssertValid(template);
            var engine = Docx.CreateEngine();
            using var stream = new MemoryStream(template);
            using var data = JsonDocument.Parse("{\"Items\":[]}");
            var error = Assert.Throws<TemplateException>(() => engine.Merge(stream, data));
            Assert.Equal(outerCode[11..], error.Field);
            Assert.Equal(outerCode, error.FieldCode);
            Assert.Contains(nestedCode.Split(' ')[0], error.Message);
            if (nestedCode.StartsWith("MERGEFIELD")) Assert.Contains("'Inner'", error.Message);
            Assert.Contains("/word/document.xml, paragraph 1", error.Location);
            Assert.Contains("unsupported", error.Message);
            Assert.Contains("outside", error.Message);
            Assert.Contains("plain cached text", error.Message);
            Assert.True(stream.CanRead);
            Assert.Equal(template, stream.ToArray());
            using var validStream = new MemoryStream(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))));
            using var validData = JsonDocument.Parse("{\"Name\":\"reused\"}");
            var valid = engine.Merge(validStream, validData);
            Docx.AssertValid(valid.Document);
            Assert.Equal("reused", Docx.Text(Docx.Read(valid.Document)));
            Assert.Empty(valid.Warnings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Deep_nested_cached_field_is_rejected(bool outerSimple)
        {
            var inner = new XElement(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, "PAGE"), Docx.Field("IF 1 = 1 yes no"));
            var contents = outerSimple ? new[] { new XElement(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, "MERGEFIELD Name"), inner) }
                : Docx.ComplexContent("MERGEFIELD Name", inner);
            var bytes = Docx.Create(Docx.P(contents));
            Docx.AssertValid(bytes);
            Assert.Throws<TemplateException>(() => Docx.Merge(bytes, "{}"));
        }

        [Fact]
        public void Instruction_nesting_and_independent_word_fields_preserve_exact_codes_and_content()
        {
            var page = Docx.Field("PAGE", "17");
            var conditional = Docx.ComplexContent("IF 1 = 1 yes no", Docx.Run("yes"));
            var contents = new[] { new XElement(WordXml.RunElementName, Docx.Character("begin"), new XElement(WordXml.InstructionTextElementName, "MERGEFIELD Name ")) }
                .Concat(Docx.ComplexContent("PAGE", Docx.Run("instruction page")))
                .Concat(new[] { new XElement(WordXml.RunElementName, Docx.Character("separate")), Docx.Run("old"), new XElement(WordXml.RunElementName, Docx.Character("end")) }).ToArray();
            var template = Docx.Create(Docx.P(contents), Docx.P(page, conditional, Docx.Field("MERGEFIELD Other")));
            Docx.AssertValid(template);
            var original = Docx.Read(template);
            var result = Docx.Merge(template, "{\"Name\":\"updated\",\"Other\":\"sibling\"}");
            var xml = Docx.Read(result.Document);
            Assert.Equal("instruction pageupdated17yessibling", Docx.Text(xml));
            Assert.Equal(original.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value), xml.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value));
            Assert.Equal(original.Descendants(WordXml.FieldCharacterElementName).Select(x => (string?)x.Attribute(WordXml.FieldCharacterTypeAttributeName)), xml.Descendants(WordXml.FieldCharacterElementName).Select(x => (string?)x.Attribute(WordXml.FieldCharacterTypeAttributeName)));
            Assert.Equal(page.ToString(), xml.Descendants(WordXml.SimpleFieldElementName).First(x => (string?)x.Attribute(WordXml.InstructionAttributeName) == "PAGE").ToString());
        }

        [Theory]
        [InlineData("PAGE")]
        [InlineData("MERGEFIELD Inner")]
        public void Complex_cached_nested_fields_are_rejected_before_merge(string nestedCode)
        {
            var template = Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", nestedCode)));
            Docx.AssertValid(template);
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{\"Name\":\"new\",\"Inner\":\"inner\"}"));
            Assert.Equal("Name", error.Field);
            Assert.Equal("MERGEFIELD Name", error.FieldCode);
            Assert.Contains(nestedCode.Split(' ')[0], error.Message);
            Assert.Contains("paragraph 1", error.Location);
            Assert.Contains("/word/document.xml", error.Location);
            Assert.Contains("plain cached text", error.Message);
            Assert.Contains("outside", error.Message);
        }

        [Theory]
        [InlineData(true, "😀A🚧B𐐀", "Z🚧")]
        [InlineData(false, "😀A🚧B𐐀", "Z🚧")]
        [InlineData(true, "A😀🚧Z", "😀")]
        [InlineData(false, "A😀🚧Z", "😀")]
        public void Unicode_scalar_boundaries_preserve_runs_codes_and_remerge(bool simple, string value, string second)
        {
            var runs = new[] { "x", "", "xx", "x", "xx" }.Select(text => new XElement(WordXml.RunElementName, Docx.Properties(), new XElement(WordXml.TextElementName, text))).ToArray();
            const string code = " MERGEFIELD Name \\* MERGEFORMAT ";
            XElement field = simple
                ? new XElement(WordXml.SimpleFieldElementName, new XAttribute(WordXml.InstructionAttributeName, code), runs)
                : Docx.P(new XElement(WordXml.RunElementName, new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, "begin")), new XElement(WordXml.InstructionTextElementName, code), new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, "separate"))), runs, new XElement(WordXml.RunElementName, new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, "end"))));
            var template = Docx.Create(simple ? Docx.P(field) : field);
            Docx.AssertValid(template);
            var original = Docx.Read(template);
            foreach (var replacement in new[] { value, second, "", "BMP text" })
            {
                var result = Docx.Merge(template, JsonSerializer.Serialize(new { Name = replacement }));
                var xml = Docx.Read(result.Document);
                Assert.Equal(replacement, Docx.Text(xml));
                Assert.Equal(original.Descendants(WordXml.RunElementName).Count(), xml.Descendants(WordXml.RunElementName).Count());
                Assert.Equal(original.Descendants(WordXml.RunPropertiesElementName).Select(x => x.ToString()), xml.Descendants(WordXml.RunPropertiesElementName).Select(x => x.ToString()));
                Assert.Equal(original.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value), xml.Descendants(WordXml.InstructionTextElementName).Select(x => x.Value));
                Assert.Equal(original.Descendants(WordXml.FieldCharacterElementName).Select(x => (string?)x.Attribute(WordXml.FieldCharacterTypeAttributeName)), xml.Descendants(WordXml.FieldCharacterElementName).Select(x => (string?)x.Attribute(WordXml.FieldCharacterTypeAttributeName)));
                if (simple) Assert.Equal(code, (string?)Assert.Single(xml.Descendants(WordXml.SimpleFieldElementName)).Attribute(WordXml.InstructionAttributeName));
                template = result.Document;
            }
        }

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
