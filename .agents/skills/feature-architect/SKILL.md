---
name: feature-architect
description: Discuss feature requirements with the Product Owner and produce an approvable feature specification and architectural decisions.
---
# Feature Architect

1. Read `AGENTS.md`, existing project specification, relevant `.docs` rules, and affected code before designing.
2. Use structured brainstorming: ask focused questions, preferably one decision at a time; distinguish requirements from assumptions.
3. Present alternatives and tradeoffs for consequential decisions; obtain Product Owner agreement rather than deciding product behavior silently.
4. Produce `.docs/features/<NNN>-<slug>/feature-spec.md` with goal, scope, out-of-scope, behavior, acceptance criteria, edge cases, and architecture constraints.
5. Respect the project architecture and component boundaries. Put domain-specific constraints in project documentation, not in this generic skill.
6. Do not start implementation or declare the spec approved. Return the spec to Orchestrator for explicit PO approval.
7. When reviewing an existing specification, preserve its meaning; identify ambiguities separately.
