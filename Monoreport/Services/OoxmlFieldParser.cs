using System.Xml.Linq;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class OoxmlFieldParser(FieldCodeParser fieldCodeParser)
    {
        private readonly FieldCodeParser _fieldCodeParser = fieldCodeParser ?? throw new ArgumentNullException(nameof(fieldCodeParser));

        public IReadOnlyList<FieldReference> Parse(XElement root, string location)
        {
            var found = new List<FieldReference>();
            var stack = new Stack<FieldFrame>();
            foreach (var node in root.Descendants())
            {
                if (node.Name == WordXml.SimpleFieldElementName)
                {
                    var field = _fieldCodeParser.Parse((string?)node.Attribute(WordXml.InstructionAttributeName) ?? string.Empty, location);
                    if (field is not null)
                    {
                        found.Add(new(field, node, null, null, null, root, location));
                    }
                }
                else if (node.Name == WordXml.FieldCharacterElementName)
                {
                    switch ((string?)node.Attribute(WordXml.FieldCharacterTypeAttributeName))
                    {
                        case WordXml.FieldBeginValue:
                            stack.Push(new(node));
                            break;
                        case WordXml.FieldSeparatorValue:
                            if (stack.Count == 0 || stack.Peek().Separator is not null)
                            {
                                throw new TemplateException("Unexpected field separator.", location: location);
                            }

                            stack.Peek().Separator = node;
                            break;
                        case WordXml.FieldEndValue:
                            if (stack.Count == 0)
                            {
                                throw new TemplateException("Field end without begin.", location: location);
                            }

                            var frame = stack.Pop();
                            var field = _fieldCodeParser.Parse(frame.Code.ToString(), location);
                            if (field is not null)
                            {
                                found.Add(new(field, null, frame.Begin, frame.Separator, node, root, location));
                            }

                            break;
                    }
                }
                else if (node.Name == WordXml.InstructionTextElementName && stack.TryPeek(out var current) && current.Separator is null)
                {
                    current.Code.Append(node.Value);
                }
            }

            if (stack.Count != 0)
            {
                throw new TemplateException("Unclosed complex field.", location: location);
            }

            var order = root.Descendants().Select((node, index) => (node, index)).ToDictionary(x => x.node, x => x.index);
            return found.OrderBy(x => order[x.Anchor]).ToArray();
        }
    }
}
