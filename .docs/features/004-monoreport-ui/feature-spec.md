# Feature 004 — MonoReport.UI Playground

## 1. Status, Objective, and Authority

1. **Status: approved for planning.** The Product Owner approved the complete Feature Spec on 2026-10-13. The Product Owner previously approved the visual/interaction mockup `monoreport-ui-mockup-v10.html` and the stale-result and failure-evidence decisions recorded in items 31, 40, 43, and 45 on 2026-10-13. Implementation still requires independent review and separate Product Owner approval of the Implementation Plan; implementation review and Product Owner acceptance remain required afterward.
2. Deliver a real, minimal, developer-oriented browser UI for generating documents with MonoReport: select a DOCX template, enter JSON, generate through the existing MonoReport HTTP API, preview the template/result, and download the generated file.
3. The approved v10 HTML mockup is the visual and interaction reference. This spec defines real behavior and supersedes mock-only simulation. Preserve its deliberately minimal design; avoid decorative, repetitive, or explanatory UI clutter.
4. Place the new frontend project `MonoReport.UI` in the **existing MonoReport repository**. The React app is not a separate product or service and has no server-side persistence.

## 2. Technology and Architecture

5. Use React, TypeScript, Ant Design, Webpack, and SWC. Use Monaco Editor for JSON editing and `docx-preview` for in-browser DOCX preview. TypeScript type checking must run separately from SWC transpilation.
6. Build a static frontend bundle. For Feature 004, support local development against `MonoReport.Api` (using a development-server proxy or equivalent same-origin setup), plus a reproducible production-mode static bundle build. Keep generated assets out of source control.
7. **Do not implement Docker image creation, `dotnet publish` integration, API static-file hosting, or packaging the bundle into `MonoReport.Api` in this feature.** Those are follow-up integration/deployment steps. The eventual target is one API-hosted static UI with no extra runtime server. Do not add a second backend.
8. The UI must call the existing public HTTP API; never access `MonoReport` Core directly or create an alternate generation implementation. Use relative `/api/...` paths from the browser. Do not hardcode an API hostname or credentials.
9. Keep UI code independent of DOCX internals: browser preview is only a visualization of returned bytes; generation is always the API's responsibility. Do not alter Core, CLI, public HTTP contracts, or template syntax to accommodate the UI.
10. Reuse the repository's existing tooling conventions where available. Do not introduce unrelated frameworks, CSS systems, application state libraries, persistence, or build automation.

## 3. Layout and Visual Behavior

11. Match `monoreport-ui-mockup-v10.html` closely: compact developer-tool look, neutral/light styling, minimal top bar, two balanced work areas, no marketing-style headers, no simulation disclaimers or filler text.
12. Left pane: a single toolbar with `Template`, `Select`, selected filename with clear action, and `Generate` at its right edge. Below, `Form / JSON` switch with **Form visible but disabled**, `Copy`, `Format`, and the JSON editor.
13. Right pane: `Template`, `Result`, `Errors` tabs; an error count when applicable; a context-appropriate `Download` action near the preview header; content filling the available preview area.
14. Desktop: adjustable **vertical splitter** between the input and preview panes. On narrow/mobile layouts, stack panes and provide an adjustable **horizontal splitter**. Preserve the approved mockup's responsive behavior, sensible minimum pane sizes, and keyboard-operable splitters.
15. Footer: compact GitHub icon and `MonoReport` linking as a single anchor to `https://github.com/montr/mr`; adjacent `v0.0.1` linking to `https://github.com/montr/mr/releases/tag/v0.0.1`; `API docs` linking to `/docs/`. Use the actual configured project version rather than a duplicated hardcoded string when the repository provides a reliable source. The initial repository URL and version are provisional product metadata, not an assumption that the release exists. No other footer clutter.
16. All UI labels are **English only** for this feature. Prefer short single-word actions: `Select`, `Generate`, `Copy`, `Format`, `Download`.
17. The product logo is temporary; a final logo redesign is out of scope. Do not spend effort redesigning it or introduce a new brand identity.

## 4. Template Selection and Preview

18. Accept one DOCX template at a time from the `Select` file picker or drag-and-drop onto the preview area. The empty state says **`Select a template`**. Only `.docx` is supported in this feature; reject other input with a clear diagnostic without replacing an already selected valid template or its result.
19. Selecting a new template switches to `Template`, resets the generated-result state and prior generation errors, and **preserves the JSON editor contents**. Clearing the template returns to the empty template state without clearing JSON.
20. Render the actual uploaded DOCX with `docx-preview` in `Template`; do not use fabricated HTML or text inferred from the filename. Release obsolete object URLs/resources and avoid stale rendering when the user selects another file while a preview is pending.
21. `Result` is unavailable until a successful generation has produced real DOCX bytes. On successful generation automatically open `Result` and preview those bytes with `docx-preview`. The user can switch freely between available `Template`, `Result`, and `Errors` tabs.
22. Preview fidelity need not be pixel-identical to Word. If preview rendering fails, display a concise warning; **do not treat a successfully generated DOCX as a generation failure and do not disable its download**. Previewing the template can fail independently of its eligibility for API generation.
23. A preview zoom toolbar is **not** part of this feature; leave its design for later if genuine usage warrants it. Do not add a paid document viewer.

## 5. JSON Editing and Generation

24. Monaco uses a light theme with JSON syntax highlighting and editing diagnostics. Initialize the editor with `{}`. Keep one editable JSON document, not a per-template cache. `Copy` copies the current editor text; `Format` pretty-prints valid JSON without changing its meaning and reports invalid syntax without overwriting the input.
25. The `Form` selector is visible but disabled. Do not implement inferred data schemas, dynamic form generation, converters, or the future Template Analyze feature. The user supplies the complete JSON payload.
26. Generation runs **only when the user presses `Generate`**. Check for a selected template and syntactically valid JSON before making the request. Show Monaco diagnostics close to their source; also show blocking errors in `Errors`.
27. Call existing `POST /api/documents/generate` using `multipart/form-data`: `template` as a file part and `data` as one raw JSON text field. Do not invent an API endpoint, alter its contract, or send the optional `culture` field unnecessarily. Preserve the existing API's invariant-culture default for this initial UI; no culture selector is required.
28. Treat HTTP 200 with the expected DOCX media type and payload as successful generation, hold the returned Blob/bytes in browser memory, preview it, and enable `Download`. Download the **actual API bytes**. Honor a safe filename from `Content-Disposition`; the current API supplies deterministic `document.docx`, which is also the fallback when a usable filename is absent. Use the format-neutral button label `Download`.
29. On a generation failure, show **real** API diagnostics in `Errors` and open that tab automatically; the five errors in the mockup are **illustrative only** and must not appear in production by default. The current generation endpoint returns one RFC Problem Details object with safe `type`, `title`, `status`, `detail`, `code`, `traceId`, and nullable `field` values. Also handle validation-style error collections when provided, HTTP/network failures, and unexpected response formats without displaying raw stack traces or unsafe HTML. Preserve multiple issues and useful details if returned; do not synthesize extra Core validation errors that the generation endpoint did not return.
30. Display an Errors count and use full available width with consistent padding, not a centered Word-page layout. JSON syntax errors should be visible both through Monaco and through the Errors tab. Keep diagnostic content readable and scrollable for longer issue lists.
31. Prevent duplicate submissions while generation is pending and show an unobtrusive busy state. Invalidate any previous generated result and disable `Result` and `Download` whenever the selected template or JSON changes, throughout generation, and after generation failure. Only a new successful generation restores `Result` and `Download`; do not introduce a previous-result state or automatically regenerate. Do not clear the editor on failures.
32. Keep ordinary template/JSON contents local to the browser and send them only for the explicit generation API request. No background uploads, analytics, autosave, localStorage, IndexedDB, or server persistence in this feature.

## 6. Integration and Configuration Boundaries

33. The existing API generation endpoint remains the sole backend for this feature. The existing `/api/documents/validate` endpoint is unchanged; do **not** implement or rename Template Analyze in Feature 004.
34. The future deployment design will serve UI at `/`, API at `/api/...`, and API docs at `/docs/`, with a `UiEnabled` switch (provisionally defaulting to true). **Do not implement that deployment switch until API hosting integration is explicitly scoped.**
35. Authentication/SSO, authorization, JWT and cookie session policy, CSRF policy for protected deployment, public-host security, rate limiting, and Docker bundling are separate decisions/features. For now use same-origin relative requests and avoid embedding tokens in frontend assets. Existing API safety limits remain in force.
36. Preserve a clean implementation path for future XLSX support, but do not support XLSX preview, upload, or generation now. Do not put `DOCX` into generic command labels just to describe the current format.

## 7. Verification and Acceptance Criteria

37. Verify one real end-to-end local flow with a valid DOCX template and JSON via `MonoReport.Api`: select, preview actual template, generate, preview the real output, download bytes, and open downloaded DOCX successfully. A mocked endpoint or simulated paper does not count as this acceptance evidence.
38. Verify selecting a second template preserves JSON, resets the old result, and previews the new template; file picker and drag-and-drop follow the same path, including rejection of unsupported files.
39. Verify JSON syntax highlighting, `Copy`, `Format`, invalid-JSON highlighting and error display, `{}` initial contents, disabled `Form`, and generation exclusively on button click.
40. Verify a real HTTP `422` through the live UI using syntactically valid but template-incompatible JSON, a rejected template/render error, and an unavailable server. The production UI must continue to block malformed JSON before HTTP and must not provide a validation bypass. Check that the Errors tab shows trustworthy details, error count, normal layout and no fabricated extra diagnostics; multiple errors are rendered when the response contains multiple issues.
41. Verify both preview failure paths: template preview failure remains informative; generated-document preview failure does not block download of successful bytes.
42. Verify responsive desktop/mobile layout, resizable splitters (pointer and keyboard), correct toolbar actions and tabs, and accessible controls/keyboard focus. Check that links and version labels match configured metadata.
43. Use **Vitest + React Testing Library** for a small, meaningful frontend test suite (roughly 5–8 focused scenarios, not a numerical target). Test request construction (DOCX and JSON multipart payload), successful generation/result/download state, real API error rendering, malformed Problem Details through a stubbed response, blocking invalid JSON without an HTTP request, and template or JSON changes clearing stale results while template changes preserve JSON. Prefer tests that detect plausible regressions; do not duplicate coverage of MonoReport Core/API business logic.
44. Provide `npm run test` (Vitest), `npm run typecheck` (TypeScript), and `npm run build` (Webpack). Do not introduce Jest, Playwright, browser E2E infrastructure, snapshot/pixel-perfect layout tests, coverage thresholds, or tests of Ant Design, Monaco, and docx-preview internals. Check the approved visual layout and actual DOCX preview manually in a browser.
45. Run the relevant existing API regression tests only if backend behavior is touched; otherwise reuse the existing API test evidence for malformed JSON and other unchanged API behavior, and verify the real API integration manually. Record which interactions were exercised with a live backend, which were checked with fixtures or stubbed responses, which relied on existing API evidence, and any environmental limitations. Do not present simulated success as real evidence.

## 8. Explicit Deferrals and Delivery

46. Out of scope: `TemplateAnalyzer`, Form mode, schema inference, XLSX, preview zoom toolbar, template/data persistence, saved sessions, accounts/SSO, metrics endpoint, API client library, MCP, localization, Docker, API-hosted static assets, and release publication.
47. Deliver tracked `MonoReport.UI` source, package/lockfile, build/dev/type-check commands, concise developer README or existing documentation updates, and focused Vitest/React Testing Library tests covering the agreed behavior. Do not commit generated build artifacts.
48. Follow repository `AGENTS.md`, applicable Architect/Developer/Reviewer/Orchestrator Skills, approved plan/review gates, and PO-only Git operation rules. **Do not stage, commit, push, tag, or publish** without separate explicit PO authorization.
