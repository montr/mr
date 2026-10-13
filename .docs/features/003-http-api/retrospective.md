# Feature 003 Retrospective — HTTP API and Validation

## Outcome

1. Delivered HTTP generation, Core/CLI validation, structured diagnostics, OpenAPI, Swagger UI, and broad automated tests; Feature 003 was accepted by the Product Owner.
2. Independent review found four substantive blockers after 296 passing tests, then a missed sibling-nested-field case after the first correction. The final review passed with an independently classified, unrelated intermittent real-Kestrel test failure.

## What worked

1. Explicit Feature Spec distinctions (HTTP statuses, stream semantics, safe diagnostic aggregation) gave Reviewer clear acceptance criteria.
2. Independent source and assertion review identified missing behavior not exposed by a passing suite.
3. Later reviews reused trustworthy test evidence, limited scope, and avoided unnecessary browser and full-suite reruns.
4. The Swagger fallback followed observed Scalar multipart upload failure and was browser-verified.

## Root causes

1. Developer handoff emphasized passing test counts without confirming that each important acceptance criterion had a concrete assertion. CLI process tests initially did not exercise `--validate`; direct Core validator cases were insufficient.
2. Fail-fast exception-oriented parser assumptions conflicted with multi-error diagnostic collection. The eventual shared `StructuralDiagnostic` design resolved this.
3. Tests covered defects in separate outer fields but missed multiple sibling nested fields within the same cached result. Container identity and cardinality were insufficiently tested.
4. Repeated full verification sometimes added little evidence, particularly after formatting-only changes; a flaky Kestrel host test added noise during unrelated parser corrections.
5. Feature records repeated approval and workflow boilerplate, making actionable evidence harder to find.

## Adopted process improvements

1. Before Developer handoff, reconcile acceptance criteria with actual assertions or justified checks in the existing implementation plan; prioritize high-risk requirements.
2. Prefer fewer strong behavior-focused tests to numerous superficial ones. Test count and pass rate are not coverage or quality metrics.
3. For aggregation and ordering, examine zero/one/many cases within and across relevant containers, with clear ordering and defect identity assertions.
4. Report changed scenarios, commands, results, evidence, and limitations concisely; keep approval records and subsequent corrections delta-based.
5. Run one final full build and normal suite before initial review. For localized corrections, use impact-based focused checks and reuse valid evidence unless materially invalidated or explicitly requested otherwise.
6. Reviewers independently inspect code and test effectiveness, reuse credible Developer evidence, and assess unrelated flaky failures without assuming success.

## Deferred

1. Collect trustworthy development-time/token metrics before attributing measured savings to process changes.
2. Consider code coverage and mutation testing later as diagnostic aids, not percentage KPIs; do not enable them now.
3. Consider a `feature-retrospective` Skill only if future retrospectives justify its overhead.

## Evidence and limitations

1. Source: Feature 003 specification, implementation plan, review findings and correction records, current project Skills, and the analysis-only retrospective of 2026-10-13.
2. No complete Codex session logs or reliable token/cost/timing measurements were available. Efficiency conclusions are qualitative; no numerical savings are claimed.
