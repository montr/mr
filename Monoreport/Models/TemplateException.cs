namespace Monoreport.Models
{
    public sealed class TemplateException(string message, string? field = null, string? fieldCode = null,
        string? location = null, string? formatter = null, Exception? inner = null) : Exception(message, inner)
    {
        public string? Field { get; } = field;

        public string? FieldCode { get; } = fieldCode;

        public string? Location { get; } = location;

        public string? Formatter { get; } = formatter;
    }
}
