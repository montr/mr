namespace MonoReport.Models
{
    public sealed class TemplateException : Exception
    {
        public TemplateException(string message, string? field = null, string? fieldCode = null,
            string? location = null, string? formatter = null, Exception? inner = null,
            string code = "generation.invalid_data") : base(message, inner)
        {
            Code = code;
            Field = field;
            FieldCode = fieldCode;
            Location = location;
            Formatter = formatter;
            Errors = Array.AsReadOnly(new[] { ToDiagnostic(this, null) });
        }

        internal TemplateException(TemplateException first, IEnumerable<GenerationDiagnostic> errors,
            bool limitReached) : base(first.Message, first.InnerException)
        {
            Code = first.Code;
            Field = first.Field;
            FieldCode = first.FieldCode;
            Location = first.Location;
            Formatter = first.Formatter;
            Errors = Array.AsReadOnly(errors.ToArray());
            LimitReached = limitReached;
        }

        public string Code { get; }
        public string? Field { get; }

        public string? FieldCode { get; }

        public string? Location { get; }

        public string? Formatter { get; }

        public IReadOnlyList<GenerationDiagnostic> Errors { get; }

        public bool LimitReached { get; }

        internal GenerationDiagnostic ToDiagnostic(string? dataPath, string? location = null) =>
            ToDiagnostic(this, dataPath, location);

        internal static GenerationDiagnostic ToDiagnostic(TemplateException error, string? dataPath,
            string? location = null)
        {
            var resolvedLocation = location ?? error.Location;
            return new(error.Code, SafeMessage(error.Code), error.Field, Part(resolvedLocation),
                resolvedLocation, error.Formatter, dataPath);
        }

        private static string? Part(string? location) => location?.Split(',', 2)[0];

        private static string SafeMessage(string code) => code switch
        {
            "template.invalid_package" => "The content is not a readable DOCX package.",
            "template.invalid_xml" => "A document part contains invalid XML.",
            "template.unsupported_structure" => "The DOCX document structure is unsupported.",
            "template.invalid_field_syntax" => "A MERGEFIELD instruction has invalid syntax.",
            "template.invalid_field_structure" => "The Word field structure is invalid.",
            "template.invalid_region" => "A collection region is invalid.",
            "template.overlapping_regions" => "Collection regions overlap or share unsupported boundaries.",
            "template.unsupported_nested_field" => "Nested Word fields inside MERGEFIELD results are unsupported.",
            "generation.invalid_format" => "A value cannot be formatted as requested.",
            "generation.unknown_formatter" => "A requested formatter is unavailable.",
            _ => "The data is incompatible with the template."
        };
    }
}
