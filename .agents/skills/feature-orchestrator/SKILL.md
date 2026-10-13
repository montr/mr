---
name: feature-orchestrator
description: Coordinate feature discussion, approved specification, planning, implementation, independent review, and Product Owner acceptance; resume from persisted state.
---

# Feature Orchestrator

1. Coordinate and delegate to Architect, Developer, and independent Reviewer. Do not substitute self-review; if independent agents are unavailable, disclose this and request a review path.
2. Assign sequential, never-reused IDs (`001`, `002`, etc.) after checking directories and available Git history. Store feature artifacts in `.docs/features/<NNN>-<slug>/`; create only needed documents: `feature-spec.md`, `implementation-plan.md`, `review.md`, `feature-status.json`.
3. Preserve project rules and uncommitted work. Do not modify controlling Skills/workflow without explicit Product Owner approval. Follow the Git Operations rules in `AGENTS.md`.

## Workflow

1. `discussion`: Architect prepares specification and works through product decisions with Product Owner. Enter `planning` only after explicit approval of the complete Feature Spec.
2. `planning`: Developer prepares implementation plan; Reviewer independently reviews it. Resolve blocking plan findings and obtain explicit Product Owner plan approval before entering `implementation`.
3. `implementation`: Developer implements approved requirements and automated tests, then supplies verification evidence. Enter `review` only when ready, disclosing failed/unexecuted checks.
4. `review`: Reviewer independently assesses conformance and evidence. **On the first `CHANGES REQUESTED`, record findings with severity, evidence, and rationale; immediately stop and report to Product Owner.** Do not automatically delegate fixes or run another review. Resume only on explicit Product Owner decisions to accept, reject, defer, or selectively authorize corrections. Record decisions; unresolved blocking findings may not silently become `PASS`. Enter `acceptance` only once blockers and required checks are resolved.
5. `acceptance`: Product Owner explicitly accepts or directs corrections. Use Developer and independent Reviewer for necessary re-verification, returning to `review` as appropriate. Enter `completed` only after explicit Product Owner acceptance.
6. For approved small technical tasks, a shortened workflow is permitted, but affected checks and tests remain required. Material changes to scope, architecture, requirements, or gates need explicit approval.

## State and records

1. `feature-status.json` contains **only** `phase`, one of `discussion`, `planning`, `implementation`, `review`, `acceptance`, `completed`. The directory identifies the feature; do not add counters, timestamps, approvals, or test outcomes to this JSON.
2. Record specification approval in `feature-spec.md`, plan review/approval in `implementation-plan.md`, and implementation review, verification, and acceptance in `review.md`. Never invent approvals or PASS; update phase only when gates are satisfied, atomically where practical.
3. On resume, inspect `feature-status.json`, relevant feature documents, Git state, and implementation. Reconcile claims with evidence; resume only incomplete authorized work and respect stop/approval gates.
4. Feature 001 historical registration: retain the original spec in `.docs/features/001-initial-implementation/feature-spec.md`, version `0.0.1`, initially in `review`; preserve requirements, translate into English if needed, and update obsolete path references. Do not invent retrospective approvals/plan or perform an audit/corrections without explicit authorization.

## Resource discipline

1. Read only relevant files/sections/diffs and valid prior evidence. Keep handoffs and feature review updates delta-based: decision, changed behavior, findings, verification, limitations, next action. Record each approval once; retain audit history without repeating boilerplate.
2. After a tool/environment failure, allow at most one focused diagnostic retry; if unresolved, stop and consult Product Owner. Do not silently replace tooling or make material architecture changes.
3. Never invent token usage or remaining Codex quotas. Browser and automated test execution policy belongs to the Developer and Reviewer Skills.
