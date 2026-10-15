using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MonoReport.Api.Services;

public sealed class ApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var generation = context.Description.RelativePath == "api/documents/generate";
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            ["template"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "Required nonempty DOCX template." }
        };
        if (generation)
        {
            properties["data"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Required text field containing one raw JSON value." };
            properties["culture"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Optional culture name; invariant when omitted or blank. Accept-Language is ignored." };
        }
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new() { Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object, Properties = properties,
                    Required = generation ? new HashSet<string> { "template", "data" } : new HashSet<string> { "template" },
                    AdditionalPropertiesAllowed = generation
                } }
            }
        };
        operation.Summary = generation ? "Generate a DOCX document" : "Analyze a DOCX template";
        operation.Description = generation
            ? "Returns raw DOCX bytes. Structural validity does not guarantee data compatibility."
            : "Returns structural diagnostics and the recursive template field model without JSON input or generation. Structurally invalid nonempty DOCX content returns 200 with valid=false and all reliably extracted fields.";
        if (generation)
        {
            var success = (OpenApiResponse)operation.Responses!["200"];
            success.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
            };
            success.Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["Content-Disposition"] = new OpenApiHeader { Description = "Attachment with safe deterministic document.docx filename.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                ["X-MonoReport-Warning-Count"] = new OpenApiHeader { Description = "Present only when generation warnings exist.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } }
            };
        }
        foreach (var (status, response) in operation.Responses!)
        {
            if (status == "200") continue;
            if (response is not OpenApiResponse concrete) continue;
            response.Description = status switch
            {
                "400" => "request.missing_input, request.empty_input, request.duplicate_input, request.invalid_input, request.invalid_multipart" + (generation ? ", data.invalid_json, culture.invalid" : ", request.unexpected_input"),
                "413" => "request.too_large (pre-application host/proxy rejection may use a host-owned response)",
                "415" => "request.unsupported_media_type",
                "422" => "template.invalid_package, template.invalid_xml, template.unsupported_structure, template.invalid_field_syntax, template.invalid_field_structure, template.invalid_region, template.overlapping_regions, template.unsupported_nested_field, generation.invalid_data, generation.invalid_format, generation.unknown_formatter",
                _ => "server.internal_error"
            };
            concrete.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object, Required = new HashSet<string> { "type", "title", "status", "detail", "code", "traceId" },
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["type"] = new OpenApiSchema { Type = JsonSchemaType.String }, ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer }, ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = response.Description }, ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                } }
            };
        }
        return Task.CompletedTask;
    }
}
