using System.Globalization;
using System.Text.Json;

namespace Monoreport.Services
{
    public sealed class FormatterRegistry(IReadOnlyDictionary<string, IValueFormatter> formatters)
    {
        private readonly Dictionary<string, IValueFormatter> _formatters = new Dictionary<string, IValueFormatter>(formatters ?? throw new ArgumentNullException(nameof(formatters)), StringComparer.OrdinalIgnoreCase);

        public void Register(string name, IValueFormatter formatter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _formatters[name] = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public string Format(JsonElement value, string name, CultureInfo culture, IReadOnlyDictionary<string, string>? options = null)
        {
            if (_formatters.TryGetValue(name, out var formatter) == false)
            {
                throw new FormatException($"Unknown formatter: {name}");
            }

            return formatter.Format(value, culture, options ?? new Dictionary<string, string>());
        }
    }
}
