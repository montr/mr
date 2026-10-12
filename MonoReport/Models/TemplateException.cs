namespace MonoReport.Models
{
    public sealed class TemplateException(string message, string? field = null, string? fieldCode = null,
        string? location = null, string? formatter = null, Exception? inner = null,
        string code = "generation.invalid_data") : Exception(message, inner)
    {
        public string Code { get; } = code;
        public string? Field { get; } = field;

        public string? FieldCode { get; } = fieldCode;

        public string? Location { get; } = location;

        public string? Formatter { get; } = formatter;
    }
}
