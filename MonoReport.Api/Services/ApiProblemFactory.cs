namespace MonoReport.Api.Services;

public sealed class ApiProblemFactory
{
    public IResult Create(HttpContext context, int status, string code, string? field = null) => Results.Problem(
        statusCode: status, type: $"urn:monoreport:error:{code}", title: status switch
        {
            400 => "Invalid request", 413 => "Request too large", 415 => "Unsupported media type",
            422 => "Document generation failed", _ => "Internal server error"
        }, detail: SafeMessage(code), extensions: new Dictionary<string, object?>
        {
            ["code"] = code, ["traceId"] = context.TraceIdentifier, ["field"] = field
        });

    public string SafeMessage(string code) => code switch
    {
        "request.missing_input" => "A required input is missing.",
        "request.empty_input" => "A required input is empty.",
        "request.duplicate_input" => "An input was supplied more than once.",
        "request.invalid_input" => "An input has the wrong representation.",
        "request.unexpected_input" => "Validation accepts only the template file.",
        "request.invalid_multipart" => "The multipart request is malformed.",
        "request.too_large" => "The whole request exceeds the configured size limit.",
        "request.unsupported_media_type" => "Use multipart/form-data.",
        "data.invalid_json" => "The data field must contain one valid JSON value.",
        "culture.invalid" => "The culture name is invalid.",
        "template.invalid_package" => "The content is not a readable DOCX package.",
        "template.invalid_xml" => "A document part contains invalid XML.",
        "template.unsupported_structure" => "The DOCX structure is unsupported.",
        "template.invalid_field_syntax" => "A template field has invalid syntax.",
        "template.invalid_field_structure" => "A Word field has invalid structure.",
        "template.invalid_region" => "A collection region is invalid.",
        "template.overlapping_regions" => "Collection regions overlap.",
        "template.unsupported_nested_field" => "A MERGEFIELD contains an unsupported nested field.",
        "generation.invalid_data" => "The data is incompatible with the template.",
        "generation.invalid_format" => "A value cannot be formatted as requested.",
        "generation.unknown_formatter" => "A requested formatter is unavailable.",
        _ => "The server could not complete the operation."
    };
}
