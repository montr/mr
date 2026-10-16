using System.Globalization;
using System.Text.Json;

namespace MonoReport.Services
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
            var formatter = Get(name);

            return formatter.Format(value, culture, options ?? new Dictionary<string, string>());
        }

        internal void Validate(string name) => Get(name);

        private IValueFormatter Get(string name)
        {
            if (_formatters.TryGetValue(name, out var formatter)) return formatter;
            throw new MonoReport.Models.UnknownFormatterException($"Unknown formatter: {name}");
        }
    }
}
