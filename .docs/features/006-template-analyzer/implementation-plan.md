# Feature 006 — Implementation Plan

## 1. Status and approval

1. The Product Owner approved this plan on 2026-10-16 before implementation.
2. The Product Owner approved a single-agent workflow without mandatory independent review. No artificial pending review gate applies.

## 2. Ordered implementation

1. Replace `TemplateValidator` with `TemplateAnalyzer`, add the public recursive analysis DTOs, and build an internal per-story analysis containing parsed fields and regions.
2. Aggregate scalar and array fields by case-sensitive name and data scope, preserving first appearance and merging repeated arrays recursively. For scalars, collect unique supported switch sequences in first-appearance order, including the empty sequence; for arrays, return no format sequences. Emit a non-fatal warning only for scalar/array collisions.
3. Preserve structural diagnostics and deterministic ordering while returning only reliably extracted fields from safe story scopes.
4. Pass analyzed original-story fields and regions into `DocumentRenderer` so generation does not independently parse them again; allow cloned collection content to be parsed recursively.
5. Replace the HTTP validation endpoint and OpenAPI contract with `POST /api/documents/analyze` while preserving multipart and Problem Details behavior.
6. Replace CLI `--validate` with `--analyze`, preserving stdout, stderr, and exit-code conventions.
7. Update affected tests and public documentation without changing the Playground.

## 3. Risks and controls

1. Region aggregation can accidentally mix data scopes; cover nested, repeated, empty, and cross-story regions with behavior-focused tests.
2. Partial malformed templates can imply false hierarchy; expose fields only where field and region structure is safe.
3. Rendering reuse can alter mutation behavior; retain element references only for original stories and reparse cloned regions.
4. Recursive OpenAPI models can regress schema generation; verify the generated document contract.
5. Data-model conflicts must remain warnings and must not change structural validity or rendering semantics.

## 4. Verification mapping

1. Core unit tests cover simple and complex fields, split instructions, dotted and case-sensitive names, ordered unique format sequences including empty sequences, repeated and nested regions, conflicts, partial results, multiple stories, stream ownership, and render-analysis reuse.
2. Existing generation regression tests cover output validity, formatting, regions, diagnostics, and caller ownership.
3. API integration and OpenAPI tests cover `/analyze`, removal of `/validate`, malformed and valid templates, multipart errors, and recursive schemas.
4. CLI tests cover `--analyze`, JSON output, help, invalid options, operational failures, and exit codes.
5. Final verification runs focused tests, the normal solution build and test suites, CLI smoke checks, and live HTTP smoke checks without browser automation.
