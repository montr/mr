using System.Text;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class FieldCodeParser
    {
        public MergeField? Parse(string code, string location)
        {
            var inspection = Inspect(code, location);
            if (inspection.Diagnostic is not null) throw inspection.Diagnostic.ToException();
            return inspection.Field;
        }

        internal FieldCodeInspection Inspect(string code, string location)
        {
            var trimmed = code.TrimStart().TrimStart('{').TrimStart();
            if (trimmed.StartsWith(WordFieldCodes.MergeField, StringComparison.OrdinalIgnoreCase) == false || (trimmed.Length > WordFieldCodes.MergeField.Length && char.IsWhiteSpace(trimmed[WordFieldCodes.MergeField.Length]) == false))
            {
                return new(null, null);
            }

            var tokenization = Tokenize(code, location);
            if (tokenization.Diagnostic is not null) return new(null, tokenization.Diagnostic);
            var tokens = tokenization.Tokens!;
            if (tokens.Count == 0 || tokens[0].Equals(WordFieldCodes.MergeField, StringComparison.OrdinalIgnoreCase) == false)
            {
                return new(null, null);
            }

            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1]) || tokens[1].StartsWith('\\'))
            {
                return Invalid("MERGEFIELD has no field name.", fieldCode: code);
            }

            var switches = new List<FieldSwitch>();
            for (var i = 2; i < tokens.Count; i += 2)
            {
                if (i + 1 >= tokens.Count || tokens[i].StartsWith('\\') == false)
                {
                    return Invalid("Invalid field switch.", tokens[1], code);
                }

                switches.Add(new(tokens[i], tokens[i + 1]));
            }

            var field = new MergeField(tokens[1], code, switches);
            if (field.IsRegion && string.IsNullOrWhiteSpace(field.Collection))
            {
                return Invalid("Region has no collection name.", field.Name, code);
            }

            return new(field, null);

            FieldCodeInspection Invalid(string detail, string? field = null, string? fieldCode = null) =>
                new(null, new("template.invalid_field_syntax", detail, field, fieldCode, location));
        }

        private static (List<string>? Tokens, StructuralDiagnostic? Diagnostic) Tokenize(string code, string location)
        {
            // Word stores braces as UI, not in instrText. Accept them for standalone parser callers too.
            var input = code.Trim();
            if (input.StartsWith('{') && input.EndsWith('}'))
            {
                input = input[1..^1];
            }

            var result = new List<string>();
            for (var i = 0; i < input.Length;)
            {
                if (char.IsWhiteSpace(input[i]))
                {
                    i++;
                    continue;
                }

                var token = new StringBuilder();
                if (input[i] == '"')
                {
                    i++;
                    while (i < input.Length && input[i] != '"')
                    {
                        if (input[i] == '\\' && i + 1 < input.Length && input[i + 1] == '"')
                        {
                            i++;
                        }

                        token.Append(input[i++]);
                    }

                    if (i == input.Length)
                    {
                        return (null, new("template.invalid_field_syntax", "Unterminated quoted field argument.",
                            FieldCode: code, Location: location));
                    }

                    i++;
                }
                else
                {
                    while (i < input.Length && char.IsWhiteSpace(input[i]) == false)
                    {
                        token.Append(input[i++]);
                    }
                }

                result.Add(token.ToString());
            }

            return (result, null);
        }
    }
}
