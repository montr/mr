# Project Rules

## Feature Workflow

1. Follow `.agents/skills/feature-orchestrator/SKILL.md` for feature development, including independent Architect, Developer, and Reviewer roles. Do not substitute Developer self-review for independent review.
2. Require explicit Product Owner approval of the complete Feature Spec before planning, of the independently reviewed Implementation Plan before implementation, and of the result before completion. On the first `CHANGES REQUESTED`, stop and await Product Owner decisions; do not automatically fix or re-review findings.
3. Keep feature artifacts under `.docs/features/<NNN>-<slug>/` with sequential, never-reused IDs. `feature-status.json` contains only `phase`; the Orchestrator owns transitions. Record approvals and verification evidence in the feature documents and reconcile persisted state with Git and files when resuming.
4. Material changes to requirements, architecture, scope, workflow, or approval gates require explicit Product Owner approval. Shortened workflows for minor work also require approval and must retain relevant tests.
5. Do not push, merge, publish, delete branches, or change controlling Skills without explicit Product Owner permission. Preserve uncommitted changes.
6. Maintain `.docs/features/backlog.md`: propose deferred requirements for Product Owner approval; do not silently discard them or create a Feature directory until selected.
7. Historical Feature 001: preserve the original specification as `.docs/features/001-initial-implementation/feature-spec.md`, version `0.0.1`, initially in `review`. Do not invent past approvals or a retrospective plan. Audit or correct it only with explicit authorization.

## Coding Guidelines

1. Implement services as instances with externally supplied constructor dependencies. Do not use public/internal static service methods, construct service dependencies internally, or introduce parameterless dependency defaults. Private static helpers are allowed; constants-only classes must not contain service logic.
2. Store per-operation data in a separate context, not reusable service state.
3. Prefer primary constructors with null validation (`?? throw`); use regular constructors when required by correct initialization.
4. Isolate DOCX-specific logic from shared APIs in anticipation of other formats such as XLSX, without premature abstractions or unapproved formats.

## Documentation Rules

1. Write Skills, project rules, specifications, plans, decisions, and other project documentation in English; Product Owner discussions may use another language.
2. Use explicitly sequential numbered lists, not bullets; keep references unambiguous.
3. Do not add code-formatting examples unless the Product Owner requests them.
