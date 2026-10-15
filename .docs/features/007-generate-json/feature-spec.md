# Feature 007 — Generate JSON from Template

**Status:** Approved
**Project:** MonoReport (`montr/monoreport`)  
**Proposed location:** `.docs/features/007-generate-json/feature-spec.md`

## 1. Goal

Let a user select a DOCX template in the existing Playground and generate an editable example JSON document from the **Feature 006 Template Analyzer** result. Make the workflow easy and safe: no inferred data values, no silent overwrites of editor content, no changes to the original template or existing generation behavior.

## 2. Scope

1. Add a **Generate JSON** action to the Playground, available when a DOCX template is selected and no conflicting operation prevents safe use.
2. Send that selected DOCX file to the existing `POST /api/documents/analyze` endpoint, using the `template` multipart field. No new HTTP endpoint or Core/CLI behavior.
3. Convert the response `fields` tree into a sample JSON document locally, then place it into the existing Monaco JSON editor after the user approves replacement when needed.
4. Reuse the existing UI error/notification conventions. Keep the Template, Result and Errors views and generation/download contracts intact.
5. Add focused unit/component tests and update only directly affected user documentation.

## 3. JSON generation rules

1. Treat the Analyzer result as authoritative for discovered field names and array nesting. Do not parse DOCX or MERGEFIELD instructions again in the browser.
2. For a `string` field, generate an empty string `""`. Do **not** infer numbers, dates, booleans, or values from `formats`, field names, or cached Word text.
3. For an `array` field, generate **one example item** with the recursively generated child fields. A collection with no child fields generates `[{}]`.
4. Preserve field-name case and first-appearance order provided by Analyzer. Split dotted scalar names into nested JSON objects according to the current `DataResolver` semantics: `Company.Name` becomes `{ "Company": { "Name": "" } }`. For dotted array names, apply the same path expansion at the current scope. Paths inside an array are relative to each array item, not the root.
5. Merge shared object prefixes, e.g. `Company.Name` and `Company.Address` share the same `Company` object. Repeated names are already consolidated by Analyzer; the frontend should be deterministic and robust to duplicates without creating duplicate keys.
6. Sample JSON is **best-effort assistance**, not a validator or a guarantee that every template field will resolve. Resolve structural overlaps deterministically by choosing the richer shape: **array > object > string**. Thus `Company` plus `Company.Name` yields `{ "Company": { "Name": "" } }`; a scalar and an array at the same path yields an array; an object and an array at the same path yields an array. Never reject JSON generation, show an error, or block insertion merely because of these conflicts. Users may edit the generated JSON manually.
7. Merge compatible child paths from competing shapes where possible; if an array wins over an object at the same path, place the object's child fields into the array's example item. A scalar losing to an object or array does not get a separate representation. Apply these rules recursively, regardless of field order, and preserve first-appearance order among retained keys.
8. Do not mutate Analyzer response objects. Format the generated JSON using the project's existing formatting conventions (readable indentation and stable order).
9. `formats` metadata is intentionally ignored for sample value creation; a future typed sample/Schema feature may use it.

Illustrative result for `Company.Name` and `Orders` (array containing `Number` and `Items`, itself an array):

```json
{
  "Company": { "Name": "" },
  "Orders": [
    {
      "Number": "",
      "Items": [
        { "Product": { "Name": "" } }
      ]
    }
  ]
}
```

## 4. UI and lifecycle

1. The action is explicit: **do not automatically analyze or replace JSON** on template selection, page load, or ordinary generation.
2. Before replacing the editor content, compare the proposed text with current content. If different and current JSON editor has nonempty content, show an explicit confirmation (e.g., "Replace current JSON with generated example?"). Cancel leaves the editor unchanged. Replacements of empty content need no confirmation.
3. On successful confirmation, use the existing JSON editor state update path. This should invalidate any previous generated Result exactly as normal JSON edits do; never leave a stale downloadable DOCX associated with new JSON.
4. Analyze the *original selected File* without altering bytes or the selected template. Do not reuse browser preview-normalized copies.
5. On failed HTTP request, invalid analysis (`valid: false`), malformed/unexpected response, do not replace JSON. Show actionable errors using existing UI conventions. For `valid: false`, include safe Analyzer `errors`/`warnings` where appropriate.
6. Prevent race conditions: if the selected template changes, the operation is cancelled/superseded, the component unmounts, or a later Generate JSON action supersedes an earlier one, discard stale responses and never overwrite current JSON. Protect the confirmation step as well (the user may change the template/editor while a confirmation is open).
7. While analysis is in progress, indicate progress and prevent accidental duplicate submissions. Do not make the existing Generate action silently invoke Analyzer.
8. Preserve existing keyboard accessibility, responsive behavior and general visual style; do not redesign the Playground or add unrelated controls.

## 5. Integration and quality

1. Implement a small, pure, independently testable `fields` → JSON conversion function and a focused API client function using the existing request/error conventions.
2. Test nested and repeated arrays, dotted names, same-prefix objects, case sensitivity, order, empty arrays of field definitions, `formats` being ignored, and valid `fields: []` producing `{}`.
3. Test deterministic best-effort conflict precedence (`array > object > string`): scalar/object (`Company` + `Company.Name`), same-name scalar/array, object/array, overlapping children, reversed occurrence order, and nested array scopes. Such conflicts must not block JSON creation or create user-facing errors.
4. Test successful analysis, server/network failure, invalid Analyzer response, confirmation/cancel, replacement and Result invalidation, as well as stale responses after template change or subsequent actions.
5. Preserve existing tests for editing JSON, formatting, generation, preview, download and error handling. Run frontend typecheck/tests/build and available targeted checks. Manual browser visual/interactive acceptance is performed by the Product Owner; no browser automation unless explicitly authorized.
6. The API and CLI contracts from Feature 006 must remain unchanged. Do not introduce server persistence, an analysis cache, custom JSON schemas, or inferred values.
7. Record actual verification evidence and any limitations. Product Owner explicitly approves final acceptance.

## 6. Exclusions

1. No Core/CLI/API feature changes beyond a minimal, separately discussed correction if an actual blocker is found.
2. No automatic form generation, JSON Schema, smart type inference, field titles, placeholder syntax (`{...}`), or support for literal dotted JSON property keys.
3. No automatic run of document generation after inserting sample JSON.
4. No redesign of README or the existing Playground UI.

## 7. Implementation workflow

1. One Codex agent; prepare a short plan and wait for Product Owner approval before edits.
2. No subagents or mandatory independent review for this bounded UI feature; flag any newly discovered high-risk boundary for discussion.
3. Do not stage, unstage, commit, push, tag, or run browser automation without Product Owner authorization.
4. On 2026-10-16 the Product Owner approved this complete Feature Spec and the implementation plan, including the single-agent workflow without a mandatory independent review gate.
5. Structural conflicts in generated sample data are auxiliary best-effort conversions and therefore produce neither errors nor warnings.
6. Analysis results that become stale after a template or JSON edit must never replace current editor data.
