using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using MonoReport.Models;

namespace MonoReport.Api.Services;

public sealed class ValidationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(TemplateAnalysisResult))
            schema.Required = new HashSet<string> { "valid", "errors", "warnings", "fields" };
        if (context.JsonTypeInfo.Type == typeof(TemplateValidationIssue))
        {
            schema.Required = new HashSet<string> { "code", "message", "field", "part", "location" };
            foreach (var name in new[] { "field", "part", "location" })
                if (schema.Properties?[name] is OpenApiSchema property) property.Type = JsonSchemaType.String | JsonSchemaType.Null;
            schema.Description = "Stable structural error codes: template.invalid_package, template.invalid_xml, template.unsupported_structure, template.invalid_field_syntax, template.invalid_field_structure, template.invalid_region, template.overlapping_regions, template.unsupported_nested_field. Analyzer warning code: template.field_kind_conflict. Safe message; nullable document context. Warnings do not invalidate a template.";
        }
        if (context.JsonTypeInfo.Type == typeof(TemplateField))
        {
            schema.Required = new HashSet<string> { "name", "type", "formats" };
            schema.Description = "A case-sensitive template field. Scalar formats contain unique ordered switch sequences; collections use type=array, formats=[], and include recursive fields.";
        }
        if (context.JsonTypeInfo.Type == typeof(TemplateFieldSwitch))
            schema.Required = new HashSet<string> { "kind", "value" };
        return Task.CompletedTask;
    }
}
