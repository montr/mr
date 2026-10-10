
# Project Rules

## Feature Workflow

1. For developing or modifying functionality, follow the mandatory workflow defined in `.agents/skills/feature-orchestrator/SKILL.md`. The Orchestrator coordinates work and delegates to `feature-architect`, `feature-developer`, and `feature-reviewer`. Do not substitute the Developer's self-review for independent review.

2. Start each new feature with a discussion of requirements and architectural decisions between the Product Owner and Architect. The Architect prepares `feature-spec.md`. Implementation must not begin until the Product Owner explicitly approves the specification.

3. After specification approval, the Developer prepares `implementation-plan.md`, including implementation steps, verification criteria, and an automated test plan. The Reviewer independently reviews the plan. Implementation may begin only after blocking findings are resolved and the Product Owner explicitly approves the plan.

4. The Developer implements the approved plan with mandatory automated tests. Prefer the TDD cycle (Red–Green–Refactor) where practical. For bug fixes, first add a regression test that reproduces the defect. Never report a check as passed unless it was actually run.

5. After implementation, the Reviewer independently verifies conformance with the Feature Spec, architecture, plan, and code and test quality. Blocking findings go back to the Developer for correction and subsequent re-review. Feature completion requires verified checks and explicit acceptance by the Product Owner.

6. Store feature documents in `.docs/features/<NNN>-<slug>/`, using sequential IDs `001`, `002`, `003`, and so on. Maintain a `feature-status.json` for every feature, recording the current phase, approvals, verification results, and next action. When resuming work, reconcile the recorded status with actual files and Git state rather than relying on JSON alone.

7. Register the original specification and existing implementation as Feature `001`. First audit the implementation against the requirements and tests. Do not mark this feature complete without verification and Product Owner acceptance.

8. Material changes to requirements, architecture, scope, workflow order, or approval gates require explicit Product Owner approval. Do not bypass the workflow or modify its controlling Skills without approval.

9. Do not push, merge, publish releases, or delete branches without explicit Product Owner permission. Preserve existing uncommitted changes.

10. A shortened workflow for minor technical tasks is allowed only with Product Owner approval. Required checks and tests for affected behavior still apply.

## Coding Guidelines

1. Implement services as class instances. Inject dependencies on other services through constructors. Do not use public or internal static service methods. Private static helper methods are allowed, but classes containing constants must not contain service logic.

2. Services must not instantiate their own service dependencies in constructors, methods, or field initializers. Do not introduce parameterless constructors that provide default dependencies. All service dependencies must be supplied externally through constructors. The dependency graph must be composed by the consuming application or tests.

3. Store per-operation data in a separate context rather than in the state of a reusable service.

4. Prefer primary constructors where possible. Initialize fields and properties from their parameters, preserving null validation with `?? throw` and external dependency injection. Use regular constructors only when the required logic cannot be correctly expressed using primary constructors and initializers.

## Documentation Rules

1. Write all project rules, Skills, specifications, plans, architecture decisions, and other project documentation in English. This does not restrict the language used in discussions with the Product Owner.

2. Use numbered lists instead of bullet lists in all documentation and documentation changes so that items can be referenced by number. Number items explicitly and sequentially; do not repeat `1.` for every item. Keep references to numbered items unambiguous within each document.

3. Do not add code-formatting examples to documentation unless explicitly requested by the Product Owner.
