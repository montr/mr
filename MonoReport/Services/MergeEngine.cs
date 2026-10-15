using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class MergeEngine(DocumentRenderer renderer, DocxStoryReader storyReader,
        TemplateAnalyzer analyzer)
    {
        private readonly DocumentRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        private readonly DocxStoryReader _storyReader = storyReader ?? throw new ArgumentNullException(nameof(storyReader));
        private readonly TemplateAnalyzer _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));

        /// <summary>Reads the template from its current position. The caller retains ownership of both inputs.</summary>
        public MergeResult Merge(Stream template, JsonDocument data, MergeOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(data);
            if (template.CanRead == false)
            {
                throw new ArgumentException("Template stream must be readable.", nameof(template));
            }

            options ??= new();
            var context = new MergeContext(data.RootElement, options);
            using (var output = new MemoryStream())
            {
                template.CopyTo(output);
                output.Position = 0;
                using (var document = _storyReader.Open(output, true))
                {
                    var analysis = _analyzer.Inspect(document);
                    var error = analysis.Diagnostics.FirstOrDefault();
                    if (error is not null) throw error.ToException();
                    foreach (var story in analysis.Stories)
                    {
                        _renderer.Render(story.Xml.Root!, story.Part.Uri.ToString(), context,
                            story.Fields, story.Regions);
                        using (var target = story.Part.GetStream(FileMode.Create, FileAccess.Write))
                        {
                            story.Xml.Save(target, SaveOptions.DisableFormatting);
                        }
                    }
                }

                return new(output.ToArray(), context.Warnings.AsReadOnly());
            }
        }
    }
}
