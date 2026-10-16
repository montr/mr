# Feature 008 — Generation Diagnostics & Missing Values

**Status:** Approved by Product Owner on 2026-10-16  
**Project:** MonoReport (`montr/monoreport`)  
**Target:** `.docs/features/008-generation-diagnostics/feature-spec.md`

## 1. Goal

Make document generation usable with partially filled example JSON: empty scalar values must not fail Word formatting; recoverable data/formatting errors should be returned together rather than one at a time; the Playground should generate plausible numeric/date sample values from the existing Analyzer metadata.

This feature spans Core, HTTP API, CLI and a small Playground change. Preserve successful document generation, existing format semantics for nonempty values, and caller stream/output ownership.

## 2. Agreed behavior

1. Missing scalar values, JSON `null`, and JSON empty string `""` render as empty field text. Do not invoke Word format switches or registered custom value formatters on an empty scalar. **Preserve `MissingValueBehavior`:** a missing property under `Error` policy remains a `generation` error; `Warning` still emits its existing warning; `null`/`""` do not themselves become missing-property errors. Do not silently remove an explicit missing-value policy.
2. A collection whose data value is missing, `null`, or `""` expands to zero items, subject to the same existing missing-property policy. A nonempty string, object, number or boolean where an array is required still produces `generation.invalid_data`.
3. Nonempty invalid scalar values remain errors (`generation.invalid_format`, `generation.invalid_data`, unknown formatter, etc.). Unknown/malformed Word formatting instructions and unregistered formatter names must remain detectable even if their input value is empty; do not hide template/configuration mistakes through the empty-value shortcut. Preserve existing error codes and meaningful formatter behavior.
4. Gather independent *recoverable generation* errors in a single operation. A field formatting/resolution error must not prevent checking later fields, collection items or other DOCX story parts, wherever continuing is safe. Never catch and suppress unexpected I/O, OpenXML/package errors, cancellation, or programming defects.
5. No generated DOCX is returned if any generation error occurs. Never replace an existing CLI output file with a partial document. Preserve successful output bytes/contract, stream ownership, and ordinary warnings.
6. Analyzer structural errors are a separate phase: if the template is structurally invalid, return **all available trustworthy structural errors** from that analysis and do **not** enter rendering. Never attempt rendering with broken region structure.
7. Diagnostics must be deterministic and actionable; retain established issue codes, safe messages, template location/part, and the responsible field. When an error happens in a collection item, include its **data path with zero-based item indices**, e.g. `Items[3].Amount` or `Orders[1].Items[3].Amount`, as distinct from the template location. Do not expose raw values, raw field instructions or filesystem paths.

## 3. Diagnostic limit and completeness

1. Collect up to **100 individual errors** per generation operation by default. The number is a named internal option/constant at one policy boundary, to support later configurability. **Do not add a public configuration setting to Core, API or CLI in Feature 008.**
2. Distinct occurrences and collection items remain separate errors even if they share a code and field name; **do not deduplicate**. Preserve encounter order. A repeated DOCX field occurrence can cause repeated errors.
3. Stop collecting errors as soon as the **100th error** is recorded. Do **not** search for a 101st error or continue rendering to determine whether additional errors exist.
4. Expose `limitReached: true` when 100 errors have been collected, including when the actual total would be exactly 100. The flag means **the maximum number of errors was reached**, not that the result is known to be incomplete. Do not expose `truncated` or claim that additional errors exist. The accompanying message may say: “Maximum of 100 errors reached; further errors may exist.” The limit notice is metadata, not an extra error counted among the 100.
5. Stop processing at a safe boundary immediately after reaching the limit, retain the no-partial-DOCX guarantee and do not invoke additional custom formatters merely to inspect further errors.

## 4. Core design and contracts

1. Keep `MergeEngine.Merge(...)` and successful `MergeResult` APIs. Introduce an operation-scoped generation-diagnostics collector (e.g., in `MergeContext`), never shared mutable state in reusable services.
2. A recoverable scalar failure records an error, inserts a temporary empty field value and proceeds. A recoverable invalid collection value records an error, skips the affected region and proceeds to safe following content. Continue within later array elements and independent story parts. Do not return the temporary document if any errors were recorded.
3. Preserve `TemplateException` compatibility where feasible: its existing first-error properties remain meaningful, and it can expose a read-only ordered collection of errors and limit/completeness metadata. If a different exception model is necessary, document and justify it in the implementation plan before coding.
4. Distinguish formatter/template-configuration errors, bad input, and fatal failures. Avoid broad `catch (Exception)` in rendering. Decide which custom formatter failures are known recoverable user-data errors versus fatal programmer errors; never silently convert arbitrary exceptions into `generation.invalid_format`.
5. Structural analysis may already produce multiple errors. Surface its existing error diagnostics with ordering intact instead of picking only the first. Structural and runtime errors must not be mixed by attempting unsafe rendering.
6. Maintain deterministic ordering consistent with the Analyzer's story traversal and source occurrence order; collection errors follow array item order. Index paths should reflect actual JSON nesting, not just field names.
7. Preserve existing render results for valid nonempty inputs, including formatting order, custom formatting behavior, repeated regions, headers/footers/footnotes/endnotes, and no repeated parse of original story parts introduced by Feature 006.

## 5. HTTP API

1. Successful `/api/documents/generate` still returns DOCX as before.
2. Generation errors return the established HTTP 422 Problem Details contract, retaining existing top-level properties for the first error and adding an ordered `errors` array with per-error `code`, `message`, `field` and optional `part`, `location`, `formatter`, `dataPath` where available. Include `limitReached` metadata from section 3. Do not leak raw user data, internal stack traces or filesystem paths.
3. For structural invalidity, include all Analyzer structural errors in `errors` without attempting rendering. Keep existing 400/413/415/500 handling, size constraints, cancellation and other API contracts unchanged.
4. Update generated OpenAPI and API contract tests for multiple errors, indexed data paths, error ordering and limit behavior. Do not change `/api/documents/analyze`.

## 6. CLI

1. Keep successful binary DOCX output unchanged. On errors, preserve exit code 1, no DOCX on stdout, and do not overwrite an existing output file.
2. Print all collected errors to stderr in deterministic order, one readable line per error, with `dataPath` when present. Preserve existing single-error behavior where practical; document the multi-error format. If limit is reached, print an additional non-error informational line stating that the maximum was reached and additional errors may exist.
3. Do not add new CLI switches for the error limit. Keep other argument/operational error exit codes and usage behavior.

## 7. Playground

1. Modify **Generate JSON**, not Analyzer: use each scalar field's ordered `formats` sequences to pick a *plausible example* when supported formatting supplies useful evidence. `number` or `general: SpellOut` → JSON number `1234.5`; `date` → ISO 8601 string `"2026-01-15T00:00:00Z"`; otherwise `""`. Prefer the first encountered recognized type-requiring format when different uses disagree, preserving Analyzer order. Do not treat `Upper`, `MERGEFORMAT` or unrelated general switches as numeric/date evidence.
2. This is a best-effort editor convenience, **not type inference, input validation, or a promise that arbitrary custom formatter sequences will succeed**. Keep existing dotted-name expansion, recursive array examples, array > object > string precedence, order, formatting, editor confirmation and stale-request protection from Feature 007.
3. Display multiple generation errors returned by the API, ideally with data paths and concise template locations, using the existing Errors view. Do not add a new UI flow or redesign. Maintain compatibility with old single-error Problem Details responses where practical.
4. Replacing native browser confirmation with an Ant Design dialog is **deferred** to the separate UI/design review, not part of Feature 008.

## 8. Verification and acceptance

1. Core tests: missing/null/`""` for unformatted, number, date, SpellOut and custom formatters; missing-property `Ignore`/`Warning`/`Error` policy; empty collection vs invalid nonempty collection; bad nonempty values; invalid formatter names even with empty data.
2. Aggregation tests: independent field errors, multiple collection elements and nested collection index paths, separate story parts, structural errors without rendering, stable ordering, identical diagnostics from different items not deduplicated, recoverable vs fatal exception boundaries, cancellation.
3. Limit tests: 99, **exactly 100**, and 101+ errors; verify `limitReached` becomes true at 100, collection stops at 100 without probing a 101st error, and the error list never exceeds the limit. Confirm no additional custom formatter calls after reaching the limit.
4. Output tests: an unsuccessful merge returns no DOCX and never overwrites existing CLI output; successful merge remains byte/semantically equivalent where previously covered, including custom formatters and source stream ownership.
5. API integration/OpenAPI: 422 `errors` array, first-error fields, data paths, limit metadata, multi-structural-error handling, sensitive-data safety and existing success contract.
6. CLI process tests: stderr aggregation, item paths, exit 1, empty binary stdout, no output replacement, limit notice, single-error behavior.
7. Playground tests: selected numeric/date/SpellOut examples, conflicting formats, plain fields, nested collections, unchanged JSON lifecycle; multi-error display and backward-compatible single-error response.
8. Run standard solution Release build/tests, frontend tests/typecheck/build, focused real API and CLI smoke tests. Record actual evidence and limitations. Manual browser acceptance remains with PO; do not run browser automation without permission.

## 9. Explicit exclusions and workflow

1. No user-configurable diagnostics limit in this feature, no errors deduplication, no server-side sample JSON generation, no JSON Schema, no new field types in Analyzer, no inferred field titles, no literal dotted JSON-key support, no compiled template IR or broad refactoring.
2. Use one Codex agent, with short implementation plan and PO approval **before edits**. No mandatory independent reviewer for this scope unless PO requests one; targeted review should focus on aggregation safety and API/CLI contracts.
3. Do not stage, unstage, commit, push, tag, or launch browser automation without PO authorization.
4. This specification was approved by the Product Owner on 2026-10-16. PO acceptance after implementation and manual testing is required before marking completed.

## 10. Decisions confirmed / still open

1. **Confirmed:** missing/null/empty scalar output, empty collections, collecting safe independent errors, all structural diagnostics, 100-error default, no deduplication, later configurability, best-effort typed JSON samples.
2. **Design judgment for implementation review:** preserve existing `MissingValueBehavior.Error` for truly absent properties; do not redefine it as “ignore missing” merely because formatting empty values is permitted.
3. **Confirmed:** `limitReached` means exactly that 100 errors were collected and processing stopped; no 101st-error probe, no `truncated` property, no public limit setting yet.
