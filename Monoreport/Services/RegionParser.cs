using System.Xml.Linq;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class RegionParser
    {
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

        public TemplateException Error(FieldReference field, string message) => new($"Region '{field.Field.Collection}': {message} ({field.Location}).", field.Field.Collection, field.Field.Code, field.Location);

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
