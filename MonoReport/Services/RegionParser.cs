using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class RegionParser
    {
        public void Validate(IReadOnlyList<FieldReference> fields)
        {
            var errors = Inspect(fields);
            if (errors.Count != 0)
                throw errors[0];
        }

        internal IReadOnlyList<TemplateException> Inspect(IReadOnlyList<FieldReference> fields)
        {
            var stack = new Stack<(FieldReference Start, int Index)>();
            var matched = new List<(Region Region, FieldReference? Parent)>();
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field.Field.IsStart) stack.Push((field, i));
                else if (field.Field.IsEnd)
                {
                    if (!stack.TryPop(out var start)) return [Error(field, "end without start")];
                    if (start.Start.Field.Collection != field.Field.Collection)
                        return [Error(field, $"end does not match start '{start.Start.Field.Collection}'")];
                    matched.Add((new(start.Start, field, start.Index, i), stack.TryPeek(out var parent) ? parent.Start : null));
                }
            }
            if (stack.TryPeek(out var unmatched)) return [Error(unmatched.Start, "start without end")];

            var errors = new List<TemplateException>();
            var footprints = new List<(Region Region, FieldReference? Parent, XElement[] Blocks)>();
            foreach (var item in matched.OrderBy(x => x.Region.First))
            {
                try
                {
                    var (first, last) = GetBlocks(item.Region);
                    var blocks = first.Parent!.Elements().SkipWhile(n => n != first).TakeWhile(n => n != last).Append(last).ToArray();
                    footprints.Add((item.Region, item.Parent, blocks));
                }
                catch (TemplateException error) { errors.Add(error); }
            }
            for (var i = 0; i < footprints.Count; i++)
            {
                for (var j = i + 1; j < footprints.Count; j++)
                {
                    var a = footprints[i];
                    var b = footprints[j];
                    if (!ReferenceEquals(a.Parent, b.Parent)) continue;
                    if (!a.Blocks.Any(left => b.Blocks.Any(right => left == right || left.Ancestors().Contains(right) || right.Ancestors().Contains(left)))) continue;
                    var location = a.Region.Start.ContextLocation;
                    errors.Add(new TemplateException($"Independent regions '{a.Region.Start.Field.Collection}' ({location}) and '{b.Region.Start.Field.Collection}' ({b.Region.Start.ContextLocation}) have an unsupported shared/overlapping-block layout. Place independent regions in separate supported paragraph or table-row ranges with no shared boundary block.", a.Region.Start.Field.Collection, a.Region.Start.Field.Code, location, code: "template.overlapping_regions"));
                }
            }
            return errors.DistinctBy(error => (error.Code, error.Field, error.FieldCode, error.Location)).ToArray();
        }

        public IReadOnlyList<Region> Parse(IReadOnlyList<FieldReference> fields)
        {
            var stack = new Stack<(FieldReference Field, int Index)>();
            var outer = new List<Region>();
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field.Field.IsStart)
                {
                    stack.Push((field, i));
                }
                else if (field.Field.IsEnd)
                {
                    if (stack.TryPop(out var start) == false)
                    {
                        throw Error(field, "end without start");
                    }

                    if (start.Field.Field.Collection != field.Field.Collection)
                    {
                        throw Error(field, $"end does not match start '{start.Field.Field.Collection}'");
                    }

                    if (stack.Count == 0)
                    {
                        outer.Add(new(start.Field, field, start.Index, i));
                    }
                }
            }

            if (stack.TryPeek(out var unmatched))
            {
                throw Error(unmatched.Field, "start without end");
            }

            return outer;
        }

        public TemplateException Error(FieldReference field, string message) => new($"Region '{field.Field.Collection}': {message} ({field.Location}).", field.Field.Collection, field.Field.Code, field.Location, code: "template.invalid_region");

        public (XElement First, XElement Last) GetBlocks(Region region)
        {
            // Prefer complete rows, including when markers are in different cells of one row.
            var firstRows = region.Start.Anchor.Ancestors(WordXml.TableRowElementName);
            foreach (var first in firstRows)
            {
                var last = region.End.Anchor.Ancestors(WordXml.TableRowElementName).FirstOrDefault(x => x.Parent == first.Parent);
                if (last is not null)
                {
                    return (first, last);
                }
            }

            var firstParagraph = region.Start.Anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
            var lastParagraph = region.End.Anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
            if (firstParagraph is not null && lastParagraph is not null && firstParagraph.Parent == lastParagraph.Parent)
            {
                return (firstParagraph, lastParagraph);
            }

            throw Error(region.Start, "markers must delimit sibling table rows or paragraphs");
        }
    }
}
