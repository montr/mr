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
2. Preserve required unit, integration, contract, and regression coverage. For document processing, test applicable OOXML validity, substitutions, formatting, repeated regions, and regressions.
3. During development, group related changes into logical units and run the smallest relevant tests only when useful. Do not require a failing-test demonstration or rerun tests after every edit.
4. When implementation is complete, run one full solution build and automated test suite before independent review. Additional full runs require invalidated evidence or a specific documented need. Finish code changes before this final run; if code changes afterward, identify affected evidence and rerun only what is necessary.
5. HTTP integration and generated OpenAPI contract tests remain mandatory where applicable. Run real-host/network checks only when relevant to hosting, transport, request-size limits, or similar changes. Do not run local browser E2E unless explicitly requested by the Product Owner; browser tests may run in GitHub Actions. Preserve existing browser artifacts/scripts.
6. Record test commands, exit codes, results and meaningful limitations in the feature's `review.md`. Keep raw logs/TRX outside Git with accessible paths where possible; never assert a check passed if it was not run. Distinguish inspection, automated, real-host, browser, and not-run evidence.
7. Update all affected user documentation and existing examples, including README, when public behavior or recommended usage changes. Check executable examples where relevant.
8. Submit a concise summary of changes, affected files, verification evidence, and unresolved issues for independent review. Follow the Git Operations rules in `AGENTS.md`.

## Efficiency and review feedback

1. Read only necessary files and sections; reuse current feature artifacts and valid prior evidence. Keep agent handoffs brief.
2. After an infrastructure/tool failure, allow at most one focused diagnostic retry. If unresolved, report the blocker via Orchestrator and stop. Do not silently switch tools, frameworks, or architecture.
3. Never invent token usage or remaining Codex quotas.
4. On `CHANGES REQUESTED`, stop. Implement only corrections explicitly authorized by the Product Owner through Orchestrator; submit them for independent re-review when directed. The feature remains in `review`.
5. Orchestrator owns phases. Record plan review and plan approval in `implementation-plan.md`; implementation verification belongs in `review.md`, not duplicated in `feature-status.json`.
6. Feature 001 historical registration does not authorize a retroactive plan or defect corrections.
