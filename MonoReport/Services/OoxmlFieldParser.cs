using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class OoxmlFieldParser(FieldCodeParser fieldCodeParser)
    {
        private readonly FieldCodeParser _fieldCodeParser = fieldCodeParser ?? throw new ArgumentNullException(nameof(fieldCodeParser));

        public void Validate(XElement root, string location)
        {
            var inspection = Inspect(root, location);
            if (inspection.Diagnostics.Count != 0)
                throw inspection.Diagnostics[0].ToException();
        }

        internal FieldInspection Inspect(XElement root, string location)
        {
            var found = new List<FieldReference>();
            var diagnostics = new List<StructuralDiagnostic>();
            var structures = new List<(XElement Anchor, string Code)>();
            var stack = new Stack<FieldFrame>();
            foreach (var node in root.Descendants())
            {
                if (node.Name == WordXml.SimpleFieldElementName)
                {
                    structures.Add((node, (string?)node.Attribute(WordXml.InstructionAttributeName) ?? string.Empty));
                    TryAdd((string?)node.Attribute(WordXml.InstructionAttributeName) ?? string.Empty, node,
                        field => found.Add(new(field, node, null, null, null, root, location)));
                }
                else if (node.Name == WordXml.FieldCharacterElementName)
                {
                    switch ((string?)node.Attribute(WordXml.FieldCharacterTypeAttributeName))
                    {
                        case WordXml.FieldBeginValue: stack.Push(new(node)); break;
                        case WordXml.FieldSeparatorValue:
                            if (stack.Count == 0 || stack.Peek().Separator is not null)
                                return Unsafe("Unexpected field separator.");
                            stack.Peek().Separator = node;
                            break;
                        case WordXml.FieldEndValue:
                            if (stack.Count == 0)
                                return Unsafe("Field end without begin.");
                            var frame = stack.Pop();
                            structures.Add((frame.Begin, frame.Code.ToString()));
                            TryAdd(frame.Code.ToString(), frame.Begin, field => found.Add(new(field, null, frame.Begin,
                                frame.Separator, node, root, location)));
                            break;
                    }
                }
                else if (node.Name == WordXml.InstructionTextElementName && stack.TryPeek(out var current) && current.Separator is null)
                    current.Code.Append(node.Value);
            }

            if (stack.Count != 0)
                return Unsafe("Unclosed complex field.");

            var order = root.Descendants().Select((node, index) => (node, index)).ToDictionary(x => x.node, x => x.index);
            var fields = found.OrderBy(x => order[x.Anchor]).ToArray();

            foreach (var outer in fields)
            {
                var result = outer.Range(false).ToHashSet();
                foreach (var nested in structures.Where(x => result.Contains(x.Anchor)).OrderBy(x => order[x.Anchor]))
                {
                    var nestedMerge = _fieldCodeParser.Inspect(nested.Code, location).Field;
                    var kind = nested.Code.TrimStart().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown";
                    var description = kind + (nestedMerge is null ? string.Empty : $" '{nestedMerge.Name}'");
                    diagnostics.Add(new("template.unsupported_nested_field", $"MERGEFIELD '{outer.Field.Name}' contains nested {description} in its cached result ({outer.ContextLocation}). Nested Word fields inside MERGEFIELD results are unsupported. Move the nested field outside the outer MERGEFIELD result or replace the outer result with plain cached text.", outer.Field.Name, outer.Field.Code, ContextLocation(nested.Anchor)));
                }
            }

            return new(fields, diagnostics, true);

            void TryAdd(string code, XElement anchor, Action<MergeField> add)
            {
                var inspection = _fieldCodeParser.Inspect(code, location);
                if (inspection.Diagnostic is not null)
                    diagnostics.Add(inspection.Diagnostic with { Location = ContextLocation(anchor) });
                else if (inspection.Field is not null) add(inspection.Field);
            }

            string ContextLocation(XElement anchor)
            {
                var paragraph = anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
                var context = location;
                if (paragraph is not null)
                    context += $", paragraph {root.Descendants(WordXml.ParagraphElementName).TakeWhile(x => x != paragraph).Count() + 1}";
                return context + $", field occurrence {root.Descendants().TakeWhile(x => x != anchor).Count() + 1}";
            }

            FieldInspection Unsafe(string message)
            {
                diagnostics.Add(new("template.invalid_field_structure", message, Location: location));
                return new(found, diagnostics, false);
            }
        }

        public IReadOnlyList<FieldReference> Parse(XElement root, string location)
        {
            var inspection = Inspect(root, location);
            if (inspection.Diagnostics.Count != 0) throw inspection.Diagnostics[0].ToException();
            return inspection.Fields;
        }
    }
}
