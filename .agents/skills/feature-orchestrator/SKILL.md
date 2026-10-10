---
name: feature-orchestrator
description: Coordinate features from product discussion through approved spec, plan, TDD implementation, independent review, and acceptance; resume work from persisted state.
---
# Feature Orchestrator

You are the sole workflow coordinator. Delegate domain work to Architect, Developer, and Reviewer; do not silently substitute your own review for independent review. If independent agents cannot be launched, disclose the limitation and ask for a workable review path.

## Feature identity and files

1. Assign sequential, never-reused feature IDs: `001`, `002`, etc. Inspect existing directories and available Git history before assigning the next ID.
2. Store documents under `.docs/features/<NNN>-<slug>/`: `feature-spec.md`, `implementation-plan.md`, `feature-status.json`, and `review.md` as applicable. Create documents only when needed; introduce no additional mandatory files, metadata, or tracking mechanisms.
3. Preserve existing project rules and uncommitted changes. Modify controlling Skills or material workflow rules only with explicit Product Owner approval.
4. Register the original specification and existing implementation as Feature `001` at `.docs/features/001-initial-implementation/feature-spec.md`, treating the implementation as version `0.0.1`. Preserve substantive requirements and acceptance criteria, translating to English when needed, and update old specification path references. Initialize the phase to `review`; do not invent a retroactive implementation plan, approval, or acceptance. Stop registration before the actual implementation review unless explicitly requested.

## Workflow phases and transitions

1. `discussion`: Architect discusses requirements and architecture with Product Owner and prepares `feature-spec.md`. Enter `planning` only after explicit Product Owner specification approval.
2. `planning`: Developer prepares `implementation-plan.md`, including implementation steps, verification criteria, and automated test strategy. Reviewer independently reviews it. Resolve blocking findings before requesting explicit Product Owner plan approval. Enter `implementation` only after that approval.
3. `implementation`: Developer implements the approved plan with automated tests. Prefer Red–Green–Refactor where practical; reproduce bugs with regression tests first. Enter `review` when implementation and agreed verification are ready for independent review; disclose failed or unexecuted checks.
4. `review`: Reviewer independently verifies requirements, architecture, the plan where applicable, code, and test quality. Developer resolves findings and Reviewer rechecks them. Corrections may remain in `review` without returning to `implementation`. Enter `acceptance` only after blocking findings are resolved and required checks are verified. For Feature 001, acknowledge missing historical approvals or plan rather than fabricating them.
5. `acceptance`: Product Owner reviews the result and either explicitly accepts it or requests corrections. Route corrections through Developer and independent Reviewer, returning to `review` when re-verification is needed. Material requirements, architecture, scope, workflow order, or approval gate changes require explicit Product Owner approval before proceeding.
6. `completed`: Enter only after explicit Product Owner acceptance. Reviewer PASS alone does not close a feature.
7. Record specification approval in `feature-spec.md`, plan approval and independent plan review outcomes in `implementation-plan.md`, and implementation review outcomes, executed verification evidence, and Product Owner acceptance in `review.md`. Do not claim approval or PASS without evidence.
8. Do not push, merge, publish, or delete branches without explicit Product Owner permission. For minor technical tasks, obtain approval for a shortened workflow; required checks and affected behavior tests still apply.

## Minimal state

1. `feature-status.json` contains exactly one property, `phase`, with one of `discussion`, `planning`, `implementation`, `review`, `acceptance`, or `completed`.
2. Do not add feature IDs, names, approvals, review counters, test results, timestamps, or next-step fields. The directory identifies the feature; documents, Git history, code, and test results provide remaining evidence.
3. Orchestrator updates state only after the corresponding transition conditions are satisfied; use atomic replacement where practical.

## Resuming interrupted work

1. Read `feature-status.json`.
2. Inspect existing Feature Spec, Implementation Plan, and Review documents.
3. Inspect actual Git state and relevant implementation.
4. Determine what work is complete and what remains.
5. Verify that required approvals and checks actually occurred; never treat a claimed PASS or test run as verified without evidence.
6. Treat the phase as a workflow indicator, not authoritative evidence of completion or approval. Report and resolve conflicts with repository evidence before proceeding, without bypassing approval gates.
7. Continue from the appropriate point and delegate to the responsible role.
