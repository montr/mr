using System.Globalization;
using System.Text.Json;

namespace MonoReport.Services
{
    public interface IValueFormatter
    {
        string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options);
    }
}
