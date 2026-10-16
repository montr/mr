# Project Rules

## Development Workflow

1. Classify work by its actual scope and risk. Explicit Product Owner workflow decisions take precedence and must be recorded in the relevant Feature document when one exists.
2. **Maintenance** covers bounded bug fixes, refactoring, documentation, and technical improvements that do not introduce a new product capability or materially change an approved public contract, architecture, or scope. One agent may implement Maintenance directly. A Feature Spec, Implementation Plan, Feature directory, Orchestrator, and independent review are not required. The agent may keep a brief working plan when useful, without a separate approval gate.
3. **Feature** covers a new user-facing or programmatic capability. Keep Feature artifacts under `.docs/features/<NNN>-<slug>/` with sequential, never-reused IDs. One agent prepares the complete Feature Spec and a concise Implementation Plan by default. Obtain explicit Product Owner approval before implementation and explicit Product Owner acceptance before completion. Independent Architect, Developer, Reviewer, and Orchestrator roles are optional rather than default gates.
4. **High-risk** is a modifier for Maintenance or Feature work involving substantial architecture, security or permissions, persistence or migration, concurrency, irreversible operations, public contracts, release workflows, binary or document integrity, or broad cross-component impact. Identifying high risk does not by itself require stopping: report the risk and propose proportionate additional checks. Obtain explicit Product Owner approval before materially changing approved requirements, architecture, scope, workflow gates, or when additional authority is required. Use independent review or subagents only when needed and agreed with the Product Owner.
5. For every route, preserve relevant quality checks and meaningful behavior-focused tests, disclose failed or unexecuted checks, update affected documentation, and protect existing user changes. A testable bug fix should normally include a regression test. Do not weaken verification merely because the workflow is lighter.
6. `.agents/skills/feature-orchestrator/SKILL.md` is an optional coordination tool for an explicitly selected complex or high-risk workflow. When it is used, its approved roles, gates, state transitions, and stop conditions apply. Otherwise, the handling agent owns the agreed workflow and records required approvals and verification evidence.
7. When an independent review is part of the agreed workflow, the first `CHANGES REQUESTED` result stops automatic correction and re-review. Record the findings and await Product Owner direction. Do not silently convert unresolved blocking findings into `PASS`.
8. `feature-status.json`, when used, contains only `phase`. Reconcile persisted state with Git, Feature documents, and implementation when resuming. Do not invent approvals, verification, or completion.
9. Do not change controlling Skills without explicit Product Owner permission. Follow the Git Operations rules below.
10. Maintain `.docs/features/backlog.md`: propose deferred requirements for Product Owner approval; do not silently discard them or create a Feature directory until selected.
11. Historical Feature 001: preserve the original specification as `.docs/features/001-initial-implementation/feature-spec.md`, version `0.0.1`, initially in `review`. Do not invent past approvals or a retrospective plan. Audit or correct it only with explicit authorization.

## Git Operations

1. The Product Owner exclusively manages Git commits and repository publishing operations.
2. Agents must not stage files, create commits, amend or rewrite commits, push, merge, rebase, delete branches, create tags, or publish releases without separate, explicit Product Owner authorization for the specific action.
3. Feature approval, implementation approval, and acceptance do not imply authorization for any Git operation listed above.
4. Agents may inspect Git status, history, and diffs. Preserve existing staged and unstaged user changes.

## Coding Guidelines

1. Implement services as instances with externally supplied constructor dependencies. Do not use public/internal static service methods, construct service dependencies internally, or introduce parameterless dependency defaults. Private static helpers are allowed; constants-only classes must not contain service logic.
2. Store per-operation data in a separate context, not reusable service state.
3. Prefer primary constructors with null validation (`?? throw`); use regular constructors when required by correct initialization.
4. Isolate DOCX-specific logic from shared APIs in anticipation of other formats such as XLSX, without premature abstractions or unapproved formats.

## Test Quality

1. Favor meaningful behavior-focused tests over raw test count or pass-rate targets. Verify acceptance criteria with assertions capable of detecting plausible defects; avoid redundant tests and coverage-number chasing.
2. Code coverage metrics may inform future gap analysis but are not quality targets; do not introduce coverage tooling or thresholds without separate Product Owner approval.

## Documentation Rules

1. Write Skills, project rules, specifications, plans, decisions, and other project documentation in English; Product Owner discussions may use another language.
2. Use explicitly sequential numbered lists, not bullets; keep references unambiguous.
3. Do not add code-formatting examples unless the Product Owner requests them.
