# Codex prompt — Feature 004

Act as the **Feature Orchestrator** for MonoReport Feature 004 (`MonoReport.UI`).

Read and follow the repository `AGENTS.md` and the existing `.agents/skills/feature-orchestrator/SKILL.md`, invoking the prescribed independent Architect, Developer, and Reviewer roles. Do not replace this workflow with a one-agent implementation.

Inputs supplied by the Product Owner:

1. `feature-spec.md` — draft Feature Spec 004, to be stored under `.docs/features/004-monoreport-ui/feature-spec.md`.
2. `monoreport-ui-mockup-v10.html` — **approved** visual and interaction reference, to be kept with the feature artifacts (or at a clearly linked repo path).

Instructions:

1. Inspect repository conventions, Feature 003's actual HTTP contract, the current feature backlog/status, and the approved mockup before proposing code. Reconcile the draft spec against real code; do not silently alter the scope or infer new API behavior.
2. Register Feature 004 according to the repository's established feature workflow. Preserve existing feature records and uncommitted changes.
3. Have the Architect review the **draft** specification for genuine ambiguities, missing acceptance evidence, and conflicts with existing architecture or mockup. Return only decisions the PO actually needs to make; do not fill this with hypothetical questions. The mockup has already been approved; do not redesign it.
4. **Stop after the specification review.** Present a concise summary of proposed clarifications, files created/updated, and any blocking PO decisions. Wait for explicit approval of the complete Feature Spec before asking the Developer for an Implementation Plan.
5. After later explicit spec approval, follow the normal Developer plan → independent Reviewer → PO plan approval → implementation → independent Reviewer → PO acceptance sequence. Never treat approval of the HTML mockup or this prompt as approval to implement.
6. Important scope: React + TypeScript + Ant Design + Webpack/SWC + Monaco + docx-preview. Ship a real locally runnable frontend that calls existing `POST /api/documents/generate`; static production build only. Docker packaging, API static hosting, `UiEnabled` runtime wiring, Template Analyze, and Form mode are out of scope.
7. Treat any simulated document rendering, fake API responses, and hardcoded example errors in the mockup as **mock-only**. No fabricated content or diagnostics in the production UI. Use actual DOCX preview, actual API bytes for download, and actual API error responses.
8. Include a compact frontend test setup using **Vitest + React Testing Library**, with meaningful cases for multipart requests, success/download, error display, invalid JSON, and template replacement. Expose `npm run test`, `npm run typecheck`, and `npm run build`. No Jest, Playwright, automated browser E2E, snapshot/pixel-perfect layout tests, or coverage thresholds. Verify the real UI and DOCX preview manually.
9. Be frugal with agent/tool/test usage: reuse credible existing evidence, avoid unrelated changes, and do not rerun full suites without reason. Maintain the repository's documented quality and approval gates.
10. Do not stage, commit, push, create tags/releases, publish packages, or perform other PO-reserved Git operations.

Report concisely in English, with specific outstanding PO decisions, if any.
