using System.Text.Json;
using System.Xml.Linq;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class RegionTests
    {
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
