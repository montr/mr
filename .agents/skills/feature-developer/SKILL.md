---
name: feature-developer
description: Plan and implement approved features with appropriate automated tests and concise verification evidence.
---

# Feature Developer

## Planning

1. Read the approved Feature Spec, `AGENTS.md`, relevant architecture rules, and affected code/tests.
2. Prepare `implementation-plan.md` with ordered steps, affected components, risks, verification commands, and mapping of each acceptance criterion to tests or a justified manual check.
3. Submit the plan for independent review through Orchestrator. Do not implement until blocking plan findings are resolved and explicit Product Owner plan approval is recorded.

## Implementation and verification

1. Follow the approved plan; escalate material deviations. Write production code and suitable automated tests in whichever order is effective. For testable bug fixes, include regression tests.
2. Prefer a small set of meaningful behavioral tests over numerous superficial or redundant tests. Test counts and pass rates do not prove requirement coverage. Each test should detect a plausible defect or verify a distinct contract; use strong assertions and risk-based boundaries. Preserve necessary unit, integration, contract, and regression coverage, including applicable OOXML validity, substitutions, formatting, and repeated regions.
3. During development, run the smallest relevant tests after meaningful changes, not every edit. For collection, ordering, or aggregation behavior, cover zero/one/many cases within the same container and across containers where relevant; avoid tests that merely mirror implementation assumptions.
4. Before initial implementation review, inspect the highest-risk requirement and its test assertions, reconcile each acceptance criterion against an actual test or justified manual check in the existing plan, and identify gaps. Run one final solution build and normal automated test suite. For subsequent localized fixes, rerun focused affected checks; repeat the full suite only if prior evidence is materially invalidated or the Product Owner explicitly requests it. Explain evidence reuse and affected scope.
5. HTTP integration and generated OpenAPI contract tests remain mandatory where applicable. Run real-host/network checks only when relevant to hosting, transport, request-size limits, or similar changes. Do not run local browser E2E unless explicitly requested by the Product Owner; browser tests may run in GitHub Actions. Preserve existing browser artifacts/scripts.
6. Record concise behavioral verification in `review.md`: changed scenarios and assertions, exact commands, outcomes, evidence paths, and limitations. Counts are supplementary, not proof of coverage. Keep raw logs/TRX outside Git where accessible; distinguish inspection, executed tests, and unexecuted checks. Never claim unexecuted checks passed.
7. Update all affected user documentation and existing examples, including README, when public behavior or recommended usage changes. Check executable examples where relevant.
8. Submit a concise summary of changes, affected files, verification evidence, and unresolved issues for independent review. Follow the Git Operations rules in `AGENTS.md`.

## Efficiency and review feedback

1. Read only necessary files and sections; reuse current feature artifacts and valid prior evidence. Keep agent handoffs brief.
2. After an infrastructure/tool failure, allow at most one focused diagnostic retry. If unresolved, report the blocker via Orchestrator and stop. Do not silently switch tools, frameworks, or architecture.
3. Never invent token usage or remaining Codex quotas.
4. On `CHANGES REQUESTED`, stop. Implement only corrections explicitly authorized by the Product Owner through Orchestrator; submit them for independent re-review when directed. The feature remains in `review`.
5. Orchestrator owns phases. Record plan review and plan approval in `implementation-plan.md`; implementation verification belongs in `review.md`, not duplicated in `feature-status.json`.
6. Feature 001 historical registration does not authorize a retroactive plan or defect corrections.
