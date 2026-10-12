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
            if (inspection.Errors.Count != 0)
                throw inspection.Errors[0];
        }

        internal FieldInspection Inspect(XElement root, string location)
        {
            var found = new List<FieldReference>();
            var errors = new List<TemplateException>();
            var structures = new List<(XElement Anchor, string Code)>();
            var stack = new Stack<FieldFrame>();
            foreach (var node in root.Descendants())
            {
                if (node.Name == WordXml.SimpleFieldElementName)
                {
                    structures.Add((node, (string?)node.Attribute(WordXml.InstructionAttributeName) ?? string.Empty));
                    TryAdd((string?)node.Attribute(WordXml.InstructionAttributeName) ?? string.Empty,
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
                            TryAdd(frame.Code.ToString(), field => found.Add(new(field, null, frame.Begin,
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
                var nested = structures.FirstOrDefault(x => result.Contains(x.Anchor));
                if (nested.Anchor is null) continue;
                MergeField? nestedMerge = null;
                try { nestedMerge = _fieldCodeParser.Parse(nested.Code, location); }
                catch (TemplateException) { }
                var kind = nested.Code.TrimStart().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown";
                var description = kind + (nestedMerge is null ? string.Empty : $" '{nestedMerge.Name}'");
                errors.Add(new TemplateException($"MERGEFIELD '{outer.Field.Name}' contains nested {description} in its cached result ({outer.ContextLocation}). Nested Word fields inside MERGEFIELD results are unsupported. Move the nested field outside the outer MERGEFIELD result or replace the outer result with plain cached text.", outer.Field.Name, outer.Field.Code, outer.ContextLocation, code: "template.unsupported_nested_field"));
            }

            return new(fields, Deduplicate(errors), true);

            void TryAdd(string code, Action<MergeField> add)
            {
                try
                {
                    var field = _fieldCodeParser.Parse(code, location);
                    if (field is not null) add(field);
                }
                catch (TemplateException error) { errors.Add(error); }
            }

            FieldInspection Unsafe(string message)
            {
                errors.Add(new TemplateException(message, location: location, code: "template.invalid_field_structure"));
                return new(found, Deduplicate(errors), false);
            }
        }

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
                                throw new TemplateException("Unexpected field separator.", location: location, code: "template.invalid_field_structure");
                            }

                            stack.Peek().Separator = node;
                            break;
                        case WordXml.FieldEndValue:
                            if (stack.Count == 0)
                            {
                                throw new TemplateException("Field end without begin.", location: location, code: "template.invalid_field_structure");
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
                throw new TemplateException("Unclosed complex field.", location: location, code: "template.invalid_field_structure");
            }

            var order = root.Descendants().Select((node, index) => (node, index)).ToDictionary(x => x.node, x => x.index);
            return found.OrderBy(x => order[x.Anchor]).ToArray();
        }

        private static IReadOnlyList<TemplateException> Deduplicate(IEnumerable<TemplateException> errors) =>
            errors.DistinctBy(error => (error.Code, error.Field, error.FieldCode, error.Location)).ToArray();
    }
}
