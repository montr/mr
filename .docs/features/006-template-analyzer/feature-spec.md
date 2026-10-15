# Feature 006 — Template Analyzer

**Status:** Approved by Product Owner on 2026-10-16  
**Repository:** `montr/monoreport`  
**Proposed location:** `.docs/features/006-template-analyzer/feature-spec.md`

## 1. Goal

Replace DOCX template validation with analysis that both reports structural problems and describes the data referenced by a template. Provide a consistent Core, HTTP API and CLI contract. Reuse the analysis already performed during document generation instead of parsing the unmodified source story parts twice.

This feature concerns the **engine**, not the Playground. JSON sample generation and UI changes are separate future work.

## 2. Scope and decisions

1. Replace the public `TemplateValidator` surface with `TemplateAnalyzer`; retain and reuse existing validation behavior, error codes and supported DOCX constructs. Do **not** create a second independent parser.
2. Replace `POST /api/documents/validate` with `POST /api/documents/analyze`, and CLI `--validate` with `--analyze`. There is no deprecated alias or compatibility requirement at this stage. The HTTP generate endpoint and normal CLI generation remain unchanged.
3. Analysis returns `valid`, `errors`, `warnings`, `fields`. Keep established public diagnostic fields, issue ordering and existing semantics; the aggregate internal term `diagnostics` may remain where appropriate.
4. Each field has `name`, `type` (`string` or `array`); arrays have a recursive `fields` array. Preserve original MERGEFIELD names, including periods and case, without inferring actual scalar types from field names.
5. Expose every unique sequence of supported MERGEFIELD formatting switches used by a field, including numeric `\#`, date `\@`, general `\*` modifiers, and their source order. The public representation is defined below; it must reflect actual supported parser semantics, not speculative type inference.
6. Do not expose `title` or cached `displayText` in this version. Word's cached MERGEFIELD result is not a reliable human-readable title. Do not add cache-text extraction just for this feature.
7. The analyzer provides one field tree per template; repeat occurrences are consolidated in the same data scope. Distinct uses and parsed switches are preserved internally for rendering.
8. No API-level caching, JSON Schema, user-facing forms, new metadata syntax, literal dotted JSON-key resolution, compiled template IR, or streaming DOCX-to-DOCX rewrite.

## 3. Public analysis result

Illustrative successful result:

```json
{
  "valid": true,
  "errors": [],
  "warnings": [],
  "fields": [
    { "name": "Company.Name", "type": "string", "formats": [[]] },
    {
      "name": "Orders",
      "type": "array",
      "formats": [],
      "fields": [
        { "name": "Number", "type": "string", "formats": [[]] },
        { "name": "Amount", "type": "string", "formats": [[
          { "kind": "number", "value": "#,##0.00" }
        ]] },
        {
          "name": "Items", "type": "array", "formats": [],
          "fields": [
            { "name": "Product.Name", "type": "string", "formats": [[]] }
          ]
        }
      ]
    }
  ]
}
```

Contract rules:

1. `valid` is `true` exactly when the existing *structural/template* error set is empty. Analysis of data requirements must not silently expand the set of templates rejected by normal generation.
2. `errors` and `warnings` preserve existing issue contracts and safe, stable codes. Do not expose file paths, raw OOXML instructions or sensitive input content.
3. `fields` is an ordered array. A scalar entry has `name`, `type: "string"`, and `formats`; an array entry has `name`, `type: "array"`, `formats: []`, and `fields` (possibly empty). Do not put `fields` on scalar entries.
4. `formats` is an ordered array of unique switch sequences. Each switch is `{ "kind": "number" | "date" | "general", "value": string }`; `number` = `\#`, `date` = `\@`, `general` = `\*`. Preserve switch order inside every sequence and sequence order by first occurrence. Include meaningful supported general switches such as `Upper`, `MERGEFORMAT`, `SpellOut` when present. No unsupported grammar is invented.
5. A scalar occurrence without supported switches contributes the empty sequence `[]`, so a scalar used only without switches has `formats: [[]]`. Remove only completely identical switch sequences. Multiple formatting variants are normal and do not produce warnings. Distinct uses and parsed switches remain preserved internally for rendering; do not add `duplicate` or `occurrences` flags.
6. Repeated arrays of the same name in the same data scope are consolidated into one array with the **union of their child fields**, ordered by first appearance. Apply the same rules recursively. The same scalar name in distinct array scopes remains represented separately.
7. Do not reject an otherwise valid template solely for fields `Company` and `Company.Name` or for any similarly ambiguous data-shape relationship. Both are reported literally. Whether a future JSON sample can represent all usages is separate from template validity.
8. If the same exact name in the same scope is used as both a scalar and an array, do not silently discard either use or invent a valid JSON shape. Report a distinct **non-fatal warning**, keeping structural validity unchanged, and use a documented, deterministic representation in `fields` (prefer the first occurrence). This warning must not change rendering semantics.
9. Use case-sensitive identities for field names and collection names, consistent with the existing path/region semantics. Marker keyword recognition may remain case-insensitive according to current parsing rules.
10. On malformed templates, return all reliably extracted fields from independently readable/valid parts while preserving normal structural diagnostics. Never fabricate hierarchy from a broken region; skip uncertain scopes. If no safe extraction is possible, return `fields: []`. Analysis must not throw for errors already represented by the existing validation result.

The analyzer warning code for scalar/array conflicts is `template.field_kind_conflict`. Formatting variants do not produce warnings. Do not extend the DTO into a detailed per-occurrence API without PO discussion.

## 4. Core behavior and reuse

1. Introduce public `TemplateAnalyzer` and analysis result models via the existing DI conventions. Replace references to the old validator in API, CLI, tests and documentation as applicable.
2. Reuse `DocxStoryReader`, `OoxmlFieldParser`, `FieldCodeParser`, `RegionParser`, and established structural rules; keep document/header/footer/footnote/endnote coverage and diagnostic ordering.
3. Build an internal analysis representation containing the parsed references, regions and document/story context needed by rendering. Keep this internal representation separate from the public JSON field tree.
4. Within a single `MergeEngine.Merge` call, pass the analysis of **original story parts** into rendering so that these parts are not independently parsed a second time. It is acceptable to reparse mutated/cloned XML inside expanded collection instances, where original element references cannot safely be reused.
5. Preserve the existing mutation and caller-ownership guarantees for input streams, successful DOCX bytes and errors. It is permissible to buffer/copy source DOCX when necessary for package access, safe repeated reads and rendering. Document whether copying is actually required; **no mandatory one-pass streaming rewrite** or full AST redesign in Feature 006.
6. Preserve generated DOCX semantics: scalar resolution, region expansion, formatting order, run formatting, warning/error behavior, and existing custom formatters.
7. Do not reuse the result of one HTTP `/analyze` request for a later `/generate` request. Each request handles its own independently supplied document; no template cache or server persistence.

## 5. HTTP API

1. Replace the current multipart validation route with `POST /api/documents/analyze`, accepting precisely one nonempty DOCX form file named `template`. Additional form inputs remain invalid as for validation.
2. Successful *execution* returns HTTP 200 JSON with `valid`, `errors`, `warnings`, `fields`, including for readable but structurally invalid templates.
3. Preserve existing 400/413/415/500 handling, Problem Details codes, multipart size limits, cancellation/stream disposal expectations, and OpenAPI semantics. Structural invalidity is a result, not HTTP 422.
4. Update OpenAPI schema and descriptions to match the new analyzer endpoint and recursive field models. Remove validation-only route/operation and stale descriptions; do not alter `/api/documents/generate`.

## 6. CLI

1. Replace validation mode `--validate` with `--analyze`, following existing argument, file, stdout/stderr and exit-code conventions.
2. Analysis writes exactly one result JSON to stdout, including `fields` and the established diagnostic fields. Invalid structural templates retain the existing nonzero exit behavior. Preserve operational error and argument error behavior.
3. Keep analysis independent of JSON input, culture and generation output options; update help and CLI docs/tests.

## 7. Quality and acceptance

1. Unit tests cover `w:fldSimple` and complex Word fields, split instructions, names with dots, case sensitivity, unique format sequences and their ordering, duplicate scalar uses, differing formatting, repeated regions with merged child fields, nested regions and multiple story parts.
2. Edge tests cover scalar/array name collision, `Company` + `Company.Name` (not a structural error), incomplete/corrupt region markup, partial trustworthy results, collection-scope isolation, and empty arrays of fields.
3. Regression tests prove existing structural validation behavior and diagnostic ordering remain unchanged; existing generate results and stream ownership are preserved.
4. A targeted test or instrumented assertion establishes that initial story references/regions are passed from analysis to render without a second independent original-part parse; reparse of cloned regions is permitted.
5. API integration tests verify actual multipart requests, successful and invalid analysis JSON, reserved error contracts, generated OpenAPI and removal of the old endpoint.
6. CLI tests verify result JSON, options/help, stdout/stderr and all exit paths.
7. Execute normal solution build/test suites and focused live HTTP/CLI smoke checks with representative sample templates. All normal project quality checks must pass.
8. Update affected Core/API/CLI documentation and README **only where necessary** to keep the current published commands and API descriptions correct. No general README reorganization.
9. Capture actual evidence and any unresolved risks. The PO approves final acceptance. Do not equate passing self-tests with an independent architecture review.

## 8. Explicit exclusions and future work

1. **Next UI feature:** Playground Generate JSON action and conflict-safe replacement of edited JSON, generated from the analyzer model. Do not modify UI in Feature 006 unless a breaking API change requires a minimal compatibility adjustment; discuss such an exception with PO first.
2. **Later JSON data support:** consider literal dotted keys (e.g., `"Product.Name"`) alongside nested objects, with a defined precedence rule. This does not change the current `DataResolver` semantics.
3. **Later metadata and forms:** explicit human-readable titles via a chosen metadata mechanism (possibly Word Content Controls or separate template metadata); optional JSON Schema/types, forms, and C# models.
4. **Later engine architecture:** consider a reusable template IR/AST and potentially streaming transformations after a separate design and measurement exercise.
5. No new public `displayText`/cached text, no inference of string/number/date from field names, and no automatic sample JSON in this feature.

## 9. Implementation approach

1. Use the project's current approved workflow, with one implementing Codex unless PO authorizes otherwise. Require a concise plan and PO approval before edits.
2. Limit review to meaningful risk boundaries: parser correctness, deterministic analysis contract, and preservation of rendering behavior. Avoid broad repeated re-audits.
3. Do not stage, commit, push, tag, or launch automated browser sessions without PO authorization.

## 10. Approval record

1. The Product Owner approved this complete Feature Spec on 2026-10-16.
2. The Product Owner approved a single-agent workflow without mandatory independent review for this feature. Do not create artificial pending review gates; retain the approved implementation, verification, and acceptance gates.
3. The Product Owner confirmed that ambiguous data-model paths such as `Company` and `Company.Name`, and scalar/array model conflicts, do not make the DOCX template structurally invalid. Model conflicts remain non-fatal warnings.
4. Before acceptance on 2026-10-16, the Product Owner approved replacing field `switches` with `formats`, collecting all unique formatting sequences in first-appearance order, retaining an empty sequence for unformatted scalar occurrences, returning empty formats for arrays, and removing mixed-formatting warnings. This approved requirement change returned the feature to implementation.
