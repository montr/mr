using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services
{
    public sealed class RegionTests
    {
        internal static XElement[] ConflictingLayout(string layout, string secondName = "B")
        {
            var startA = Docx.Field("MERGEFIELD TableStart:A");
            var endA = Docx.Field("MERGEFIELD TableEnd:A");
            var startB = Docx.Field("MERGEFIELD TableStart:" + secondName);
            var endB = Docx.Field("MERGEFIELD TableEnd:" + secondName);
            return layout switch
            {
                "paragraph" => new[] { Docx.P(startA, endA, startB, endB) },
                "row" => new[] { Docx.Table(Docx.Row(startA, endA, startB, endB)) },
                "boundary-paragraph" => new[] { Docx.P(startA), Docx.P(endA, startB), Docx.P(endB) },
                "boundary-row" => new[] { Docx.Table(Docx.Row(startA), Docx.Row(endA, startB), Docx.Row(endB)) },
                "ancestor-paragraph" => new[] { Docx.Table(new XElement(WordXml.TableRowElementName, new XElement(WordXml.TableCellElementName, Docx.P(startA, endA), Docx.P(startB, endB)))) },
                "ancestor-table" => new[] { Docx.Table(new XElement(WordXml.TableRowElementName, new XElement(WordXml.TableCellElementName, Docx.P(startA, endA), Docx.Table(Docx.Row(startB, endB)), Docx.P()))) },
                _ => throw new ArgumentException(layout)
            };
        }

        public static IEnumerable<object[]> ConflictingCases()
        {
            foreach (var layout in new[] { "paragraph", "row", "boundary-paragraph", "boundary-row", "ancestor-paragraph", "ancestor-table" })
            foreach (var sameName in new[] { true, false })
                yield return new object[] { layout, sameName };
        }

        [Theory]
        [MemberData(nameof(ConflictingCases))]
        public void Independent_footprint_conflicts_have_original_context_and_preserve_input(string layout, bool sameName)
        {
            var second = sameName ? "A" : "B";
            var template = Docx.Create(ConflictingLayout(layout, second));
            Docx.AssertValid(template);
            var engine = Docx.CreateEngine();
            using var input = new MemoryStream(template);
            using var data = System.Text.Json.JsonDocument.Parse("{\"A\":[{}],\"B\":[{}]}");
            var error = Assert.Throws<TemplateException>(() => engine.Merge(input, data));
            Assert.Equal("A", error.Field);
            Assert.Equal("MERGEFIELD TableStart:A", error.FieldCode);
            Assert.Contains("'A'", error.Message);
            Assert.Contains("'" + second + "'", error.Message);
            Assert.Contains("paragraph", error.Location);
            Assert.Contains("/word/document.xml", error.Location);
            Assert.Contains("field occurrence", error.Message);
            Assert.Contains("unsupported shared/overlapping-block", error.Message);
            Assert.Contains("separate supported", error.Message);
            Assert.Contains("no shared boundary", error.Message);
            if (layout.Contains("row") || layout.Contains("ancestor")) Assert.Contains("row 1", error.Location);
            Assert.True(input.CanRead);
            Assert.Equal(template, input.ToArray());
            using var good = new MemoryStream(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))));
            using var goodData = System.Text.Json.JsonDocument.Parse("{\"Name\":\"fresh\"}");
            var result = engine.Merge(good, goodData);
            Docx.AssertValid(result.Document);
            Assert.Equal("fresh", Docx.Text(Docx.Read(result.Document)));
        }

        [Theory]
        [InlineData("{}", 1)]
        [InlineData("{\"Parent\":null}", 1)]
        [InlineData("{\"Parent\":[]}", 1)]
        [InlineData("{\"Parent\":[{}]}", 1)]
        [InlineData("{}", 2)]
        [InlineData("{\"Parent\":null}", 2)]
        [InlineData("{\"Parent\":[]}", 2)]
        [InlineData("{\"Parent\":[{\"Parent\":[]}]}", 2)]
        public void Nested_sibling_conflicts_reject_even_when_parent_does_not_render(string json, int depth)
        {
            var contents = new List<XElement>();
            for (var i = 0; i < depth; i++) contents.Add(Docx.P(Docx.Field("MERGEFIELD TableStart:Parent")));
            contents.AddRange(ConflictingLayout("paragraph"));
            for (var i = 0; i < depth; i++) contents.Add(Docx.P(Docx.Field("MERGEFIELD TableEnd:Parent")));
            var template = Docx.Create(contents.ToArray());
            Docx.AssertValid(template);
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, json));
            Assert.Contains("'A'", error.Message);
            Assert.Contains("'B'", error.Message);
            Assert.Contains("paragraph " + (depth + 1), error.Location);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void Same_named_disjoint_and_nested_regions_preserve_fields_and_complete_formatting(int count)
        {
            var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD Header")),
                Docx.P(Docx.Field("MERGEFIELD TableStart:Items"), Docx.Field("MERGEFIELD Name", properties: Docx.Properties()), Docx.Field("MERGEFIELD TableEnd:Items")),
                Docx.P(Docx.Field("MERGEFIELD TableStart:Items")),
                Docx.P(Docx.Field("MERGEFIELD TableStart:Items"), Docx.Field("MERGEFIELD Name", properties: Docx.Properties()), Docx.Field("MERGEFIELD TableEnd:Items")),
                Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")), Docx.P(Docx.Field("MERGEFIELD Header")));
            var items = Enumerable.Range(0, count).Select(i => new { Name = "outer" + i, Items = new[] { new { Name = "child" + i } } }).ToArray();
            var output = Docx.Merge(template, System.Text.Json.JsonSerializer.Serialize(new { Header = "header", Items = items }));
            var xml = Docx.Read(output.Document);
            Assert.Equal("header" + string.Concat(items.Select(x => x.Name)) + string.Concat(Enumerable.Range(0,count).Select(i => "child" + i)) + "header", Docx.Text(xml));
            Assert.DoesNotContain("TableStart", xml.ToString());
            Assert.DoesNotContain("TableEnd", xml.ToString());
            Assert.Equal(2 + 2 * count, xml.Descendants(WordXml.SimpleFieldElementName).Count());
            Assert.All(xml.Descendants(WordXml.RunPropertiesElementName), p => Assert.True(XNode.DeepEquals(Docx.Properties(), p)));
            Assert.All(xml.Descendants(WordXml.SimpleFieldElementName), f => Assert.Contains("MERGEFIELD", (string?)f.Attribute(WordXml.InstructionAttributeName)));
        }

        [Fact]
        public void Independent_regions_sharing_paragraph_are_rejected()
        {
            var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableStart:A"), Docx.Field("MERGEFIELD Name"), Docx.Field("MERGEFIELD TableEnd:A"), Docx.Field("MERGEFIELD TableStart:B"), Docx.Field("MERGEFIELD Name"), Docx.Field("MERGEFIELD TableEnd:B")));
            Docx.AssertValid(template);
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{\"A\":[{\"Name\":\"one\"}],\"B\":[{\"Name\":\"two\"}]}"));
            Assert.Contains("'A'", error.Message);
            Assert.Contains("'B'", error.Message);
            Assert.Contains("paragraph 1", error.Location);
            Assert.Contains("/word/document.xml", error.Location);
            Assert.Contains("separate", error.Message);
            Assert.Contains("shared boundary", error.Message);
            Assert.Equal("MERGEFIELD TableStart:A", error.FieldCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        public void Repeats_rows_and_preserves_header_and_formatting(int count)
        {
            var template = Docx.Create(Docx.Table(Docx.Row(Docx.Run("Header")), Docx.Row(Docx.Field("MERGEFIELD TableStart:Items")), Docx.Row(Docx.Field("MERGEFIELD Name", properties: Docx.Properties()), Docx.Field("MERGEFIELD Quantity"), Docx.Field("MERGEFIELD Price \\# \"0.00\"")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Items"))));
            var json = JsonSerializer.Serialize(new { Items = Enumerable.Range(1, count).Select(i => new { Name = "Item" + i, Quantity = i, Price = 100.5m }) });
            var result = Docx.Merge(template, json);
            var xml = Docx.Read(result.Document);
            var rows = xml.Descendants(WordXml.TableRowElementName).ToArray();
            Assert.Equal(count + 1, rows.Length);
            Assert.Equal("Header", Docx.Text(rows[0]));
            for (var i = 1; i <= count; i++)
            {
                Assert.Equal("Item" + i + i + "100.50", Docx.Text(rows[i]));
                Assert.True(XNode.DeepEquals(Docx.Properties(), Assert.Single(rows[i].Descendants(WordXml.RunPropertiesElementName))));
            }

            Assert.DoesNotContain("TableStart:", xml.ToString());
            Assert.DoesNotContain("TableEnd:", xml.ToString());
            Assert.Equal(count * 3, xml.Descendants(WordXml.SimpleFieldElementName).Count());
        }

        [Fact]
        public void Supports_start_and_end_inside_the_data_row()
        {
            var row = new XElement(WordXml.TableRowElementName, new XElement(WordXml.TableCellElementName, Docx.P(Docx.Field("MERGEFIELD TableStart:Items"), Docx.Field("MERGEFIELD Name"), Docx.Field("MERGEFIELD TableEnd:Items"))));
            var result = Docx.Merge(Docx.Create(Docx.Table(row)), "{\"Items\":[{\"Name\":\"One\"},{\"Name\":\"Two\"}]}");
            var xml = Docx.Read(result.Document);
            Assert.Equal(2, xml.Descendants(WordXml.TableRowElementName).Count());
            Assert.Equal("OneTwo", Docx.Text(xml));
            Assert.Equal(2, xml.Descendants(WordXml.SimpleFieldElementName).Count());
        }

        [Fact]
        public void Nested_collections_restore_parent_and_root_context()
        {
            var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableStart:Orders")), Docx.P(Docx.Field("MERGEFIELD Number")), Docx.P(Docx.Field("MERGEFIELD TableStart:Items")), Docx.P(Docx.Field("MERGEFIELD Name"), Docx.Field("MERGEFIELD Quantity")), Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")), Docx.P(Docx.Field("MERGEFIELD Number")), Docx.P(Docx.Field("MERGEFIELD TableEnd:Orders")), Docx.P(Docx.Field("MERGEFIELD Number")));
            var result = Docx.Merge(template, """
                {"Number":"ROOT","Orders":[
                  {"Number":"A","Items":[{"Name":"X","Quantity":2},{"Name":"Y","Quantity":3}]},
                  {"Number":"B","Items":[]},
                  {"Number":"C","Items":[{"Name":"Z","Quantity":4}]}]}
                """);
            Assert.Equal("AX2Y3ABBCZ4CROOT", Docx.Text(Docx.Read(result.Document)));
            Assert.DoesNotContain("TableStart", Docx.Read(result.Document).ToString());
        }

        [Fact]
        public void Nested_table_rows_and_multiple_independent_regions()
        {
            var table = Docx.Table(Docx.Row(Docx.Field("MERGEFIELD TableStart:Orders")), Docx.Row(Docx.Field("MERGEFIELD Number")), Docx.Row(Docx.Field("MERGEFIELD TableStart:Items")), Docx.Row(Docx.Field("MERGEFIELD Name")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Items")), Docx.Row(Docx.Field("MERGEFIELD Number")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Orders")), Docx.Row(Docx.Field("MERGEFIELD TableStart:Other")), Docx.Row(Docx.Field("MERGEFIELD Name")), Docx.Row(Docx.Field("MERGEFIELD TableEnd:Other")));
            var result = Docx.Merge(Docx.Create(table), """
                {"Orders":[{"Number":"A","Items":[{"Name":"X"},{"Name":"Y"}]}],"Other":[{"Name":"Z"}]}
                """);
            Assert.Equal("AXYAZ", Docx.Text(Docx.Read(result.Document)));
            Assert.Equal(5, Docx.Read(result.Document).Descendants(WordXml.TableRowElementName).Count());
        }

        [Theory]
        [InlineData("TableEnd:Items", null, "end without start")]
        [InlineData("TableStart:Items", null, "start without end")]
        [InlineData("TableStart:Items", "TableEnd:Orders", "does not match")]
        public void Reports_region_structure_errors(string first, string? second, string expected)
        {
            var p = Docx.P(Docx.Field("MERGEFIELD " + first));
            if (second is not null)
            {
                p.Add(Docx.Field("MERGEFIELD " + second));
            }

            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(p), "{}"));
            Assert.Contains(expected, error.Message);
            Assert.NotNull(error.Field);
            Assert.NotNull(error.Location);
        }

        [Fact]
        public void Rejects_non_array_collection()
        {
            var template = Docx.Create(Docx.P(Docx.Field("MERGEFIELD TableStart:Items")), Docx.P(Docx.Field("MERGEFIELD Name")), Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")));
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(template, "{\"Items\":123}"));
            Assert.Contains("must be an array", error.Message);
            Assert.Equal("Items", error.Field);
        }
    }
}
