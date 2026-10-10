
# Product Backlog

Future capabilities identified during the initial MonoReport specification and subsequent development.

The product architecture principles below record approved product direction. Planned capabilities are not approved Feature Specs; their detailed scope, priority, and implementation require Product Owner approval through the Feature Workflow.

## Product Architecture — Multiple Document Formats

1. MonoReport is intended to support multiple document formats, not only DOCX.
2. Microsoft Word (DOCX) and Microsoft Excel (XLSX) are target formats; XLSX support is planned, not implemented.
3. Design shared components and public contracts with future XLSX support in mind.
4. Keep format-specific parsing, template processing, and rendering isolated.
5. Reuse common functionality where appropriate, including data resolution and formatting.
6. Do not assume DOCX and XLSX must use identical template syntax.
7. Avoid premature abstractions or refactoring the existing DOCX engine solely for future Excel support. Implementation of additional formats requires an approved Feature.

## Planned Capabilities

### Excel Template Engine

Status: Planned, not implemented. Detailed scope requires a future approved Feature Spec.

1. Generate XLSX workbooks from templates and structured data.
2. Evaluate Excel-native approaches to fields, repeated regions, and formatting.
3. Preserve workbook structure, formulas, styles, and other relevant Excel features.
4. Reuse suitable shared components without tightly coupling XLSX to DOCX.
5. Define detailed behavior and acceptance criteria in a future Feature Spec.

### Conditional Rendering

Support conditional blocks in document templates, including boolean values, missing values, and comparisons.

Constraints:
1. Do not execute arbitrary code from templates.
2. Keep business calculations outside the template engine.

### HTTP API

Provide an ASP.NET Core API for rendering documents from templates and JSON data.

Constraints:
1. Keep the core engine independent of the HTTP layer.
2. Define the API contract when the feature is planned.
3. Provide an OpenAPI specification for all public HTTP endpoints.
4. Document request and response schemas, authentication where applicable, errors, and supported document formats.
5. Provide interactive API documentation through Swagger UI or an equivalent OpenAPI-compatible interface.
6. Prefer generating OpenAPI documentation from actual API contracts.
7. Verify that documentation remains consistent with the implemented API.
8. Design the API contract to accommodate DOCX and future XLSX without unnecessary breaking changes.

### Word Add-in

Provide a Microsoft Word Add-in for creating and editing templates.

Capabilities:
1. Browse available data fields and collections.
2. Insert MERGEFIELD fields.
3. Configure field formatting.
4. Insert and manage collection regions.
5. Hide technical field-code details from template authors.

### Extended Formatting

Extend the formatter system with additional formatting capabilities and currency support.

Exact formatter names, currencies, and behavior are to be defined in future Feature Specs.

### Additional Output Formats

Explore HTML and PDF generation as potential future extensions.

This is exploratory and is not part of the currently approved engine scope.

## Backlog Management

1. Do not assign feature IDs to backlog items.
2. Do not treat backlog items as approved requirements.
3. When the Product Owner selects an item for implementation, create the next sequential Feature directory and start the standard workflow.
4. Preserve the original backlog entry or link it to the resulting Feature.
5. Add newly identified future capabilities here only with Product Owner approval.


## Technical Improvements

### C# Code Style and Automated Formatting

Establish consistent, readable C# code formatting across the project.

1. Review the previously established coding conventions.
2. Define formatting rules in `.editorconfig`.
3. Evaluate `dotnet format`, Roslyn analyzers, and additional tooling for rules not supported by standard formatting.
4. Configure Codex to follow and verify the agreed conventions.
5. Prevent unrelated mass-formatting changes in feature branches.
6. Validate the configuration against representative existing source files before applying it project-wide.

Status: Deferred. Requires Product Owner approval before implementation.
