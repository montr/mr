using System.Text;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class FieldCodeParser
    {
        public MergeField? Parse(string code, string location)
        {
            var trimmed = code.TrimStart().TrimStart('{').TrimStart();
            if (trimmed.StartsWith(WordFieldCodes.MergeField, StringComparison.OrdinalIgnoreCase) == false || (trimmed.Length > WordFieldCodes.MergeField.Length && char.IsWhiteSpace(trimmed[WordFieldCodes.MergeField.Length]) == false))
            {
                return null;
            }

            var tokens = Tokenize(code, location);
            if (tokens.Count == 0 || tokens[0].Equals(WordFieldCodes.MergeField, StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            if (tokens.Count < 2 || string.IsNullOrWhiteSpace(tokens[1]) || tokens[1].StartsWith('\\'))
            {
                throw new TemplateException("MERGEFIELD has no field name.", fieldCode: code, location: location);
            }

            var switches = new List<FieldSwitch>();
            for (var i = 2; i < tokens.Count; i += 2)
            {
                if (i + 1 >= tokens.Count || tokens[i].StartsWith('\\') == false)
                {
                    throw new TemplateException("Invalid field switch.", tokens[1], code, location);
                }

                switches.Add(new(tokens[i], tokens[i + 1]));
            }

            var field = new MergeField(tokens[1], code, switches);
            if (field.IsRegion && string.IsNullOrWhiteSpace(field.Collection))
            {
                throw new TemplateException("Region has no collection name.", field.Name, code, location);
            }

            return field;
        }

        private static List<string> Tokenize(string code, string location)
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
                        throw new TemplateException("Unterminated quoted field argument.", fieldCode: code, location: location);
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

            return result;
        }
    }
}
