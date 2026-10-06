using System.Text.Json;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class MergeEngineTests
    {
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
