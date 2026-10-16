# Feature 008 — Implementation Plan

**Status:** Approved by Product Owner  
**Date:** 2026-10-16

## 1. Workflow

1. The Product Owner approved the complete Feature Spec on 2026-10-16.
2. The Product Owner approved a single-agent workflow without mandatory independent plan review for this feature.
3. The Product Owner approved this implementation plan on 2026-10-16. Material deviations return to the Product Owner for approval.

## 2. Ordered implementation

1. Add a public immutable generation diagnostic model and extend `TemplateException` with an ordered read-only error collection and `LimitReached`, while retaining its existing first-error message, code, field, field-code, location, formatter, and catch compatibility.
2. Add an operation-scoped collector to `MergeContext` with one named internal limit of 100. Recording the 100th error immediately sets `LimitReached`; renderer control flow stops at the next safe boundary without probing or invoking work for a 101st error.
3. Track the current JSON data path independently from template location, including zero-based indices through nested collections. Do not include raw values, raw field instructions, or filesystem paths in public diagnostics.
4. Refine empty-value handling so missing, `null`, and `""` render as empty scalars without invoking custom formatters or value-dependent Word switches, while still validating formatter names and Word switch syntax. Preserve missing-property `EmptyString`, `Warning`, and `Error` policy behavior.
5. Treat missing, `null`, and `""` collections as zero items. Record a recoverable `generation.invalid_data` for other non-array values, remove the affected temporary region, and continue only when below the diagnostic limit.
6. Refactor rendering around narrowly scoped recovery of expected `TemplateException` generation failures. Insert temporary empty scalar results or skip invalid regions, continue through later fields, items, and stories in encounter order, and let unexpected formatter exceptions, I/O, OpenXML/package failures, cancellation, and programming defects escape unchanged.
7. Make `MergeEngine` return no document whenever collected runtime errors exist. Surface all trustworthy Analyzer structural diagnostics without rendering, preserve their order, and keep valid rendering, warnings, source ownership, and original-story parse reuse unchanged.
8. Extend HTTP 422 Problem Details with ordered `errors` and `limitReached`, retaining top-level first-error properties and all existing success and non-422 contracts. Update the OpenAPI generation-error schema without changing `/analyze`.
9. Extend CLI generation failure output to one deterministic line per error plus the specified informational limit line, while preserving exit code 1, empty stdout, single-error readability, and write-after-success behavior so an existing output file cannot be replaced by a partial DOCX.
10. Update Playground diagnostics to display optional data path and concise template context, while remaining compatible with old single-error Problem Details. Update Generate JSON to choose `1234.5` for the first recognized number or SpellOut format, `"2026-01-15T00:00:00Z"` for the first recognized date format, and `""` otherwise, preserving existing tree, ordering, confirmation, and stale-request behavior.
11. Update README and affected API/CLI examples only for implemented public diagnostic, empty-value, multi-error, and Generate JSON behavior.

## 3. Safety and contract controls

1. The collector belongs to one merge operation; reusable services receive no mutable per-operation state.
2. Recovery catches only known `TemplateException` instances at field and region boundaries. Arbitrary custom formatter exceptions are fatal and are never relabeled as invalid user data.
3. Limit checks occur immediately after every recorded error and propagate an internal stop signal through nested rendering and story traversal. No field resolution, custom formatter call, collection enumeration, or story rendering begins after error 100.
4. Rendering continues in an in-memory copy only. `MergeEngine` materializes output bytes solely after structural and runtime diagnostics are empty; CLI writes a file solely after a successful merge result.
5. Diagnostics preserve story, source occurrence, and array item order without deduplication. First-error compatibility is derived from the first item in the same ordered collection.

## 4. Verification mapping

1. Core field and formatter tests cover unformatted, number, date, SpellOut, custom formatter, missing/null/empty values, all missing-value policies, invalid nonempty values, and unknown formatters with empty input.
2. Region and renderer tests cover empty and invalid collections, independent fields, repeated and nested items with indexed data paths, separate story parts, deterministic ordering, duplicate occurrences, and no unsafe structural rendering.
3. Boundary tests cover 99, exactly 100, and 101-or-more potential errors. Assertions verify a maximum of 100 entries, `LimitReached` at exactly 100, and an observing formatter proving that no 101st candidate is evaluated.
4. Fatal-boundary tests verify that unexpected custom formatter exceptions, cancellation, and operational failures are not aggregated or suppressed.
5. Merge and CLI tests verify that failures expose no DOCX, stdout stays empty, and missing/new/existing output files are not created or replaced; existing success, OOXML validity, source ownership, and semantic output regressions remain covered.
6. API integration and OpenAPI tests verify ordered multi-error 422 responses, first-error compatibility, structural errors, indexed paths, limit metadata, safe content, raw DOCX success, and unchanged 400/413/415/500 behavior.
7. CLI unit and process tests verify ordered lines, data paths, the limit notice, exit codes, single-error behavior, and output protection.
8. Playground unit and component tests verify number/date/SpellOut examples, conflicting format precedence, plain and nested fields, multi-error display, legacy single-error responses, and unchanged JSON lifecycle protections.
9. Final verification runs focused tests during development, then the Release solution build and full backend test suites, frontend tests, typecheck and production build, plus focused real API and CLI smoke tests. Browser automation remains excluded; actual commands and outcomes are recorded in `review.md`.

## 5. Risks

1. Recovery at the wrong XML boundary could corrupt temporary OOXML or skip later independent content; tests must cover fields adjacent to and nested inside regions across stories.
2. A limit check after rather than during nested unwinding could invoke additional formatters; an observing formatter call count is required at the 100 boundary.
3. Extending `TemplateException` and Problem Details can break strict consumers; preserve existing constructors/properties and top-level first-error fields, and add only optional aggregate metadata.
4. Data-path state can leak across nested items when exceptions unwind; push/pop must use guaranteed cleanup and tests must cover errors before and after nested regions.
5. Empty values must skip value-dependent formatting without hiding unknown switches or registrations; validation and execution must remain explicitly separated.
