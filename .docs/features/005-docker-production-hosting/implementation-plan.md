# Feature 005 — Implementation Plan

**Status:** Approved
**Date:** 2026-10-15

## 1. Approval and review path

1. On 2026-10-15, the Product Owner approved this implementation plan together with the complete Feature Spec and the single-Codex experimental workflow.
2. The approved workflow waives independent Architect and Reviewer agent cycles for this Feature. Codex must stop and consult the Product Owner if implementation reveals a material security, public-contract, binary-integrity, scope, or architectural issue.
3. README instructions will be added only after their corresponding local startup scenarios have been executed successfully.

## 2. Ordered implementation steps

1. Add ASP.NET Core static-file and SPA hosting while preserving API, documentation, error, upload, and documentation-disabled behavior.
2. Integrate a clean lockfile-based frontend production build and publish inclusion into the API project, with an explicit Docker-only switch that skips redundant UI compilation without skipping asset inclusion.
3. Inject an explicit development or validated release version into the frontend without changing package metadata for releases.
4. Add behavior-focused host and publish tests for static assets, SPA fallback exclusions, documentation-disabled routes, and publish contents.
5. Add a root multi-stage Dockerfile, focused Docker build context, and Compose configuration using the GHCR image with image and host-port overrides.
6. Add GitHub Actions validation and release workflows covering .NET and frontend checks, Docker build and bounded runtime smoke checks, SemVer validation, controlled image tags, multi-platform publication, and manifest verification.
7. Run focused tests, production publish, published-host smoke checks, Docker build/run checks where tooling is available, and the final solution/frontend suites.
8. Only after successful scenario verification, add concise README instructions for development, non-Docker publish, Docker, Compose, GHCR, ports, and release tags.
9. Record exact verification evidence and remaining remote or Product Owner checks in `review.md`, then present the result for Product Owner acceptance.

## 3. Affected components

1. `MonoReport.Api` hosting and publish configuration.
2. `MonoReport.Api.Tests` HTTP integration and publish-contract tests.
3. `MonoReport.UI` build-time version metadata and existing version-display tests where applicable.
4. Root Docker, Compose, and ignore configuration.
5. `.github/workflows` validation and release automation.
6. `README.md` after verified startup scenarios only.
7. Feature 005 records under `.docs/features/005-docker-production-hosting/`.

## 4. Risks and controls

1. SPA fallback could hide backend errors or missing assets; test reserved prefixes, file-like paths, frontend routes, and disabled documentation explicitly.
2. Publish integration could rebuild unnecessarily or package stale assets; clean the UI output, use `npm ci`, make publish the trigger, and test the resulting publish tree.
3. Release metadata could diverge across UI and image tags; validate the Git tag once and pass the same normalized version into the UI and OCI metadata.
4. Docker smoke checks could hang or provide false confidence; use bounded readiness retries and assert status, content type, response body, runtime user, and runtime tool absence.
5. Remote GHCR visibility and GitHub-hosted multi-architecture results cannot be proven locally; record them as pending until the Product Owner pushes and performs the release.

## 5. Verification and acceptance mapping

1. Acceptance criteria 1 and 2: release publish plus real published-host HTTP checks and automated host tests for UI, hashed assets, API, documentation, fallback boundaries, and disabled documentation.
2. Acceptance criterion 3: frontend configuration inspection plus local Webpack development startup/proxy check when practical without browser automation.
3. Acceptance criterion 4: local Docker build/run smoke checks, non-root inspection, representative DOCX operation, and runtime checks for excluded build tools and sources.
4. Acceptance criterion 5: Compose configuration validation and startup using an overridable image; unauthenticated GHCR pull remains pending until first publication and public visibility confirmation.
5. Acceptance criteria 6 and 7: workflow inspection and local script-equivalent checks; actual PR/main and release runs, published architectures, tag behavior, and release blocking remain pending remote verification.
6. Acceptance criterion 8: frontend tests/builds for development fallback and stable/prerelease metadata derived from validated tags.
7. Acceptance criterion 9: execute documented startup paths first, then compare README commands with the verified commands.
8. Acceptance criterion 10: preserve Product Owner-only Git and release operations and request explicit final acceptance after review evidence is complete.
