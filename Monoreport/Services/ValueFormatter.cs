using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class ValueFormatter(FormatterRegistry formatters)
    {
        private const string WordInitialLetterPattern = @"\b\p{L}";
        private const string LetterPattern = @"\p{L}";

        private readonly FormatterRegistry _formatters = formatters ?? throw new ArgumentNullException(nameof(formatters));

        public string Format(JsonElement value, MergeField field, MergeOptions options, string location)
        {
            var active = "value";

            try
            {
                var empty = value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
                var text = empty ? string.Empty : value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString()!,
                    JsonValueKind.Number => value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => throw new FormatException("An object or array cannot be rendered as a scalar field.")
                };
                options.FieldFormatters.TryGetValue(field.Name, out var binding);
                if (binding is not null)
                {
                    active = binding.Name;
                    if (empty == false)
                    {
                        text = _formatters.Format(value, binding.Name, options.Culture, binding.Options);
                    }
                }

                foreach (var item in field.Switches)
                {
                    active = $"{item.Name} {item.Argument}";
                    // Validate even when the field is empty, so template mistakes are always diagnosed.
                    switch (item.Name)
                    {
                        case WordFieldCodes.GeneralFormatSwitch:
                            text = item.Argument.ToUpperInvariant() switch
                            {
                                WordFieldCodes.UpperFormat => options.Culture.TextInfo.ToUpper(text),
                                WordFieldCodes.LowerFormat => options.Culture.TextInfo.ToLower(text),
                                WordFieldCodes.CapsFormat => Regex.Replace(text, WordInitialLetterPattern, m => options.Culture.TextInfo.ToUpper(m.Value)),
                                WordFieldCodes.FirstCapFormat => FirstCap(text, options.Culture),
                                WordFieldCodes.MergeFormat => text,
                                WordFieldCodes.SpellOutFormat => empty ? string.Empty
                                    : _formatters.Format(value, RubSpellOutFormatter.FormatterName, options.Culture),
                                _ => throw new FormatException($"Unsupported Word formatting switch: {item.Argument}")
                            };
                            break;
                        case WordFieldCodes.NumberFormatSwitch:
                            if (empty == false)
                            {
                                text = decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture).ToString(item.Argument, options.Culture);
                            }

                            break;
                        case WordFieldCodes.DateFormatSwitch:
                            if (empty == false)
                            {
                                text = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces).ToString(item.Argument, options.Culture);
                            }

                            break;
                        default:
                            throw new FormatException($"Unsupported Word switch: {item.Name}");
                    }
                }

                return text;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                throw new TemplateException($"Cannot format field '{field.Name}' using '{active}'. Field code: {field.Code}. {ex.Message}", field.Name, field.Code, location, active, ex);
            }
        }

        private static string FirstCap(string text, CultureInfo culture)
        {
            var match = Regex.Match(text, LetterPattern);
            return match.Success ? text[..match.Index] + culture.TextInfo.ToUpper(match.Value) + text[(match.Index + match.Length)..] : text;
        }
    }
}
