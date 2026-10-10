
# Product Backlog

Future capabilities identified during the initial MonoReport specification and subsequent development.

These items are proposals, not approved Feature Specs. Their scope, priority, and implementation details require Product Owner approval.

## Planned Capabilities

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
