# Feature 009 — Playground Examples Implementation Plan

Status: Approved by the Product Owner on 2026-10-16

## 1. Implementation

1. Publish a static `examples.json` registry and the authoritative sample template and JSON as UI resources. Load them with ordinary browser `fetch`; do not add server endpoints, application caching, or a general loader framework.
2. Load a selected example's template and JSON concurrently and commit them atomically. Keep content revisions separate from Generate, Generate JSON, and example-load request identities so stale responses cannot overwrite newer edits or uploads.
3. Add the Examples selector, Custom and modified states, confirmation through `window.confirm`, and contextual Template and Result toolbars. Preserve Template, Result, Errors, Generate, Generate JSON, Monaco, DOCX preview, drag-and-drop, responsive sizing, and splitter behavior.
4. Keep the existing Result invalidation policy. Add exact-byte downloads for the current template and last successful result, with the result action removed from its old location.
5. Update the README and backlog reference. Use one agent without feature-orchestrator, independent review, browser automation, or Git operations.

## 2. Verification

1. Component tests cover registry/default loading, atomic failure, confirmation, Custom state, modified indication, stale selection protection, editing and upload during loads, Generate/load intersections, both download flows, and existing tab/action regressions.
2. Run `npm test -- --run`, `npm run typecheck`, and `npm run build` from `MonoReport.UI`.
3. The Product Owner manually checks Chrome and Safari startup, preview, example switching, Custom upload, Generate, Errors, downloads, narrow layout, and splitter interaction.

## 3. Acceptance mapping

1. Acceptance criteria 1–9 and the automated portion of criterion 11 are covered by focused `App` component tests.
2. Criterion 10 is covered by existing and extended component, preview, Monaco, and splitter regressions plus the final typecheck and production build.
3. The browser portion of criterion 11 is reserved for Product Owner manual acceptance and is not claimed as executed by the implementation agent.
