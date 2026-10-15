# Feature 006 — Implementation Review and Verification

## 1. Workflow decision

1. On 2026-10-16 the Product Owner approved a single-agent workflow without mandatory independent review.
2. The implementation therefore proceeds directly from verified implementation to Product Owner acceptance without an artificial independent-review gate.
3. Before initial acceptance, the Product Owner approved the material public-contract change from one `switches` projection to all unique `formats` sequences and returned the feature to implementation. The revised result is verified below and supersedes the earlier pre-acceptance evidence where noted.

## 2. Implemented behavior

1. Core exposes `TemplateAnalyzer` and immutable analysis result models containing structural diagnostics and an ordered recursive field tree.
2. Scalar fields and collection regions are consolidated case-sensitively within their data scopes. Repeated arrays merge child fields in first-appearance order.
3. Supported number, date, and general switches are exposed as unique format sequences. Format sequences follow first appearance, preserve their internal switch order, and include an empty sequence for unformatted scalar occurrences; arrays expose no format sequences.
4. Multiple formatting variants are normal and produce no warning. Scalar/array name collisions produce the non-fatal `template.field_kind_conflict` warning. Ambiguous paths such as `Company` and `Company.Name` remain structurally valid.
5. Invalid stories do not hide trustworthy fields from independently valid stories, and unsafe region hierarchies are not projected.
6. `MergeEngine` passes analyzed original-story field and region references into rendering. Cloned collection content may still be parsed recursively.
7. HTTP now exposes only `POST /api/documents/analyze` for analysis; CLI now exposes `--analyze`. Playground behavior is unchanged.

## 3. Automated verification

1. `dotnet build MonoReport.slnx -c Release --no-restore` passed with zero warnings and zero errors.
2. `dotnet test MonoReport.slnx -c Release --no-build --no-restore` passed: 233 Core tests, 65 CLI tests, and 47 API tests.
3. Core tests cover simple and complex fields, fragmented instructions, dotted and case-sensitive names, unique format-sequence ordering and deduplication, empty format sequences, repeated and nested regions, empty collection field sets, model conflicts, partial multi-story results, stream ownership, generation regressions, and the single parse of an original story before rendering.
4. API tests cover real multipart analysis, invalid analysis results, Problem Details, recursive OpenAPI format-array models, field JSON shape, absence of the old public `switches` property, and removal of `/api/documents/validate`.
5. CLI tests cover `--analyze`, exact `formats` JSON output, absence of `switches`, help, invalid arguments, safe operational failures, stdout/stderr behavior, and exit codes.
6. `git diff --check` and `bash -n scripts/container-smoke.sh` passed.
7. After the approved `formats` requirement change, `dotnet build MonoReport.slnx -c Release --no-restore` again passed with zero warnings and errors, and `dotnet test MonoReport.slnx -c Release --no-build --no-restore` again passed all 233 Core, 65 CLI, and 47 API tests.

## 4. Smoke verification

1. Release CLI analysis of `Samples/sample.template.docx` returned `valid: true`, no warnings, the `Materials` array with `formats: []`, unformatted scalar fields with `formats: [[]]`, and both number and `SpellOut` sequences for `Totals.Amount` in first-appearance order.
2. A real Development Kestrel host received multipart `POST /api/documents/analyze` for the sample and returned HTTP 200 with the same analysis model.
3. No browser automation was run.

## 5. Limitations and acceptance

1. Cached MERGEFIELD display text, JSON sample generation, Playground changes, API caching, JSON Schema, literal dotted-key resolution, and compiled template IR remain excluded as specified.
2. No Git staging, commit, push, tag, or release operation was performed by the agent. The pre-existing staged Feature Spec was preserved.
3. On 2026-10-16 the Product Owner explicitly accepted Feature 006 — Template Analyzer.
4. The Product Owner manually verified `POST /api/documents/analyze` and confirmed that the field tree, arrays, unique `formats`, `warnings: []`, and `valid: true` conform to the approved specification.
5. Feature 006 is completed.
