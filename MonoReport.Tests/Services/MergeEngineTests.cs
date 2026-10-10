using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;
using MonoReport.Services;

namespace MonoReport.Tests.Services
{
    public sealed class MergeEngineTests
    {
        private sealed class ObserveFormatter : IValueFormatter
        {
            public int Calls { get; private set; }
            public string Format(JsonElement value, System.Globalization.CultureInfo culture, IReadOnlyDictionary<string, string> options)
            {
                Calls++;
                return value.GetString()!;
            }
        }

        public static IEnumerable<object[]> InvalidStories()
        {
            foreach (var part in new[] { "/word/document.xml", "/word/header1.xml", "/word/footer1.xml", "/word/footnotes.xml", "/word/endnotes.xml" })
            foreach (var nested in new[] { true, false })
                yield return new object[] { part, nested };
        }

        [Theory]
        [MemberData(nameof(InvalidStories))]
        public void Every_original_story_is_preflighted_before_any_formatter_or_empty_region(string part, bool nested)
        {
            var invalid = nested ? new[] { Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE")) } : RegionTests.ConflictingLayout("paragraph");
            var template = Docx.CreateStories(part, invalid);
            Docx.AssertValid(template);
            var observer = new ObserveFormatter();
            var engine = Docx.CreateEngine(new FormatterRegistry(new Dictionary<string, IValueFormatter> { ["SpellOut"] = observer }));
            using var input = new MemoryStream(template);
            using var data = JsonDocument.Parse("{\"Name\":\"ready\",\"Empty\":[],\"A\":[],\"B\":[]}");
            var error = Assert.Throws<TemplateException>(() => engine.Merge(input, data));
            Assert.StartsWith(part, error.Location);
            Assert.Contains("paragraph", error.Location);
            Assert.Equal(0, observer.Calls);
            Assert.True(input.CanRead);
            Assert.Equal(template, input.ToArray());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Preflight_rejection_never_mutates_original_xml(bool nested)
        {
            var invalid = nested ? new[] { Docx.P(Docx.NestedResult(true, false, "MERGEFIELD Name", "PAGE")) } : RegionTests.ConflictingLayout("boundary-paragraph");
            var xml = Docx.Read(Docx.Create(new[] { Docx.P(Docx.Field("MERGEFIELD Earlier")) }.Concat(invalid).ToArray()));
            var snapshot = new XDocument(xml);
            var renderer = new DocumentRenderer(new DataResolver(), new OoxmlFieldParser(new FieldCodeParser()), new RegionParser(), new ValueFormatter(new FormatterRegistry(new Dictionary<string,IValueFormatter>())), new CachedResultWriter());
            Assert.Throws<TemplateException>(() => renderer.Preflight(xml.Root!, "/word/document.xml"));
            Assert.True(XNode.DeepEquals(snapshot, xml));
        }

        [Fact]
        public void All_supported_stories_merge_preserve_sibling_fields_and_reuse_engine()
        {
            var template = Docx.CreateStories(null);
            Docx.AssertValid(template);
            var original = Docx.ReadStories(template);
            var observer = new ObserveFormatter();
            var engine = Docx.CreateEngine(new FormatterRegistry(new Dictionary<string,IValueFormatter> { ["SpellOut"] = observer }));
            foreach (var name in new[] { "first😀", "second🚧" })
            {
                using var stream = new MemoryStream(template);
                using var data = JsonDocument.Parse(JsonSerializer.Serialize(new { Name = name, Empty = Array.Empty<object>(), Items = new[] { new { Item = "material" } } }));
                var result = engine.Merge(stream, data);
                Docx.AssertValid(result.Document);
                var stories = Docx.ReadStories(result.Document);
                Assert.Equal(5, stories.Count);
                foreach (var (uri, xml) in stories)
                {
                    Assert.Equal(name + "7material", Docx.Text(xml));
                    Assert.DoesNotContain("TableStart", xml.ToString());
                    var fields = xml.Descendants(WordXml.SimpleFieldElementName).ToArray();
                    Assert.Equal(3, fields.Length);
                    Assert.All(xml.Descendants(WordXml.RunPropertiesElementName), p => Assert.True(XNode.DeepEquals(Docx.Properties(), p)));
                    Assert.Equal("MERGEFIELD Name \\* SpellOut", (string?)fields[0].Attribute(WordXml.InstructionAttributeName));
                    Assert.Equal(original[uri].Descendants(WordXml.SimpleFieldElementName).First(x => (string?)x.Attribute(WordXml.InstructionAttributeName) == "PAGE").ToString(), fields[1].ToString());
                }
                Assert.Empty(result.Warnings);
            }
            Assert.Equal(10, observer.Calls);
        }

        [Fact]
        public void Injected_services_can_be_reused_without_leaking_merge_context()
        {
            var engine = Docx.CreateEngine();
            var template = Docx.Create(
                Docx.P(Docx.Field("MERGEFIELD TableStart:Items")),
                Docx.P(Docx.Field("MERGEFIELD Name")),
                Docx.P(Docx.Field("MERGEFIELD TableEnd:Items")),
                Docx.P(Docx.Field("MERGEFIELD Name")));
            var options = new MergeOptions { MissingValues = MissingValueBehavior.Warning };

            using (var firstTemplate = new MemoryStream(template))
            {
                using (var firstData = JsonDocument.Parse("""{"Items":[{}],"Name":"First"}"""))
                {
                    var first = engine.Merge(firstTemplate, firstData, options);
                    Assert.Single(first.Warnings);
                    Assert.Equal("First", Docx.Text(Docx.Read(first.Document)));
                    Docx.AssertValid(first.Document);
                }
            }

            using (var secondTemplate = new MemoryStream(template))
            {
                using (var secondData = JsonDocument.Parse("""{"Items":[{"Name":"Item"}],"Name":"Second"}"""))
                {
                    var second = engine.Merge(secondTemplate, secondData, options);
                    Assert.Empty(second.Warnings);
                    Assert.Equal("ItemSecond", Docx.Text(Docx.Read(second.Document)));
                    Docx.AssertValid(second.Document);
                }
            }
        }
    }
}
