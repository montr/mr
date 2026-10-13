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
                throw errors[0].ToException();
        }

        internal IReadOnlyList<StructuralDiagnostic> Inspect(IReadOnlyList<FieldReference> fields)
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

            var errors = new List<StructuralDiagnostic>();
            var footprints = new List<(Region Region, FieldReference? Parent, XElement[] Blocks)>();
            foreach (var item in matched.OrderBy(x => x.Region.First))
            {
                var blocks = InspectBlocks(item.Region);
                if (blocks.Diagnostic is null)
                {
                    var (first, last) = blocks.Blocks!.Value;
                    var footprint = first.Parent!.Elements().SkipWhile(n => n != first).TakeWhile(n => n != last).Append(last).ToArray();
                    footprints.Add((item.Region, item.Parent, footprint));
                }
                else errors.Add(blocks.Diagnostic);
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
                    errors.Add(new("template.overlapping_regions", $"Independent regions '{a.Region.Start.Field.Collection}' ({location}) and '{b.Region.Start.Field.Collection}' ({b.Region.Start.ContextLocation}) have an unsupported shared/overlapping-block layout. Place independent regions in separate supported paragraph or table-row ranges with no shared boundary block.", a.Region.Start.Field.Collection, a.Region.Start.Field.Code, location));
                }
            }
            return errors;
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
                        throw Error(field, "end without start").ToException();
                    }

                    if (start.Field.Field.Collection != field.Field.Collection)
                    {
                        throw Error(field, $"end does not match start '{start.Field.Field.Collection}'").ToException();
                    }

                    if (stack.Count == 0)
                    {
                        outer.Add(new(start.Field, field, start.Index, i));
                    }
                }
            }

            if (stack.TryPeek(out var unmatched))
            {
                throw Error(unmatched.Field, "start without end").ToException();
            }

            return outer;
        }

        private static StructuralDiagnostic Error(FieldReference field, string message) => new("template.invalid_region", $"Region '{field.Field.Collection}': {message} ({field.Location}).", field.Field.Collection, field.Field.Code, field.Location);

        public (XElement First, XElement Last) GetBlocks(Region region)
        {
            var inspection = InspectBlocks(region);
            if (inspection.Diagnostic is not null) throw inspection.Diagnostic.ToException();
            return inspection.Blocks!.Value;
        }

        private static ((XElement First, XElement Last)? Blocks, StructuralDiagnostic? Diagnostic) InspectBlocks(Region region)
        {
            // Prefer complete rows, including when markers are in different cells of one row.
            var firstRows = region.Start.Anchor.Ancestors(WordXml.TableRowElementName);
            foreach (var first in firstRows)
            {
                var last = region.End.Anchor.Ancestors(WordXml.TableRowElementName).FirstOrDefault(x => x.Parent == first.Parent);
                if (last is not null)
                {
                    return ((first, last), null);
                }
            }

            var firstParagraph = region.Start.Anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
            var lastParagraph = region.End.Anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
            if (firstParagraph is not null && lastParagraph is not null && firstParagraph.Parent == lastParagraph.Parent)
            {
                return ((firstParagraph, lastParagraph), null);
            }

            return (null, Error(region.Start, "markers must delimit sibling table rows or paragraphs"));
        }
    }
}
