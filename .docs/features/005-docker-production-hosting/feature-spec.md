# Feature 005 — Production Hosting, Docker & GitHub Container Registry

**Status:** Approved
**Date:** 2026-10-15
**Repository:** `montr/monoreport`

## Approval

1. On 2026-10-15, the Product Owner explicitly approved the complete Feature Spec, the single-Codex experimental workflow, and the implementation plan proposed in the discussion.
2. The Product Owner directed that README changes be made only after the corresponding startup scenarios have been verified.

## 1. Goal

Distribute MonoReport as a single, ready-to-run application with the React Playground, HTTP API, and Swagger/OpenAPI served from one ASP.NET Core origin. Support non-Docker production publishing, local Docker/Compose operation, and automatic verification and release publishing to GHCR.

## 2. Scope and architecture

1. **Development unchanged:** `MonoReport.UI` runs via Webpack Dev Server on port 3000 with HMR; `/api`, `/docs`, and `/openapi` proxy to the independently running ASP.NET Core API (default `127.0.0.1:5080`). No ASP.NET-to-Webpack proxy is introduced.
2. **Production outside Docker:** a normal `dotnet publish` of `MonoReport.Api` produces one publish directory containing backend binaries and built frontend assets. Starting the published API serves the Playground at `/`, API at `/api`, Swagger at `/docs`, and OpenAPI at `/openapi` from the same origin.
3. **Production inside Docker:** one container uses the exact same ASP.NET Core hosting implementation and published application. No Node.js, SDK, test tooling, or frontend sources are required at runtime.
4. **Compose:** one application service, no database or other dependencies. The default Compose experience uses the published `ghcr.io/montr/monoreport` image, not a local source build.
5. **CI/CD:** GitHub Actions validates code, tests, frontend and Docker behavior on PRs and `main`; version tags additionally publish validated multi-platform images to GHCR.

## 3. Functional requirements

### 3.1 Production HTTP hosting

1. Serve `index.html`, versioned frontend static assets, and their correct content types through ASP.NET Core static-file handling.
2. Preserve existing `/api/documents` contracts, Problem Details/error handling, upload size settings, `/docs`, `/openapi/v1.json`, and `MonoReport:DocumentationEnabled` behavior.
3. For non-file frontend paths outside reserved backend URL prefixes, return the SPA entry point to support direct navigation and future frontend routing.
4. Unrecognized `/api/**`, `/docs/**`, and `/openapi/**` paths must not fall back to `index.html`; preserve meaningful backend results (typically HTTP 404). Missing file-like paths (for example `/missing.js`) must not return SPA HTML.
5. Frontend requests remain relative/same-origin; production must not require a separate development proxy, frontend server, or CORS workaround.
6. When documentation is disabled, `/docs` and `/openapi` are not served and must not silently become SPA routes.

### 3.2 Publish contract

1. `dotnet publish MonoReport.Api/MonoReport.Api.csproj -c Release` builds the production UI from a clean checkout, using a project-defined compatible Node.js runtime (Node 22.12+ or suitable pinned newer LTS), `npm ci`, and `npm run build`, and includes the resulting `dist` contents as `wwwroot/**` in publish output.
2. The UI publish integration is declared in build/publish configuration (for example MSBuild targets), rather than relying on manual copying into source-controlled `MonoReport.Api/wwwroot`.
3. The integration must avoid stale frontend assets and repeated or unintended frontend rebuilds within a single publish workflow.
4. Docker may build the frontend in a dedicated Node stage and supply the same generated assets to the .NET publish stage; a deliberate build property may skip **only** redundant frontend installation/build, while retaining publish inclusion. No separate, conflicting production hosting implementation is introduced.
5. Development `dotnet build`, `dotnet test`, and normal backend debugging should not unexpectedly require an npm installation or trigger an expensive frontend build; the full frontend build belongs to the production publish contract.
6. Generated `dist`, `wwwroot` artifacts, and publish output are not committed.

### 3.3 Docker and Compose

1. Root `Dockerfile` uses multi-stage Node frontend build, .NET 10 SDK publish, and ASP.NET Core 10 runtime stages; pin compatible major versions, and ensure reproducible lockfile-based dependency installation.
2. Final container runs as a non-root user, listens on TCP 8080 by default, and runs without persistent writable application storage. Avoid unnecessary OS packages.
3. Root `.dockerignore` excludes source-control metadata, local build outputs, `node_modules`, `dist`, test artifacts, IDE files, secrets, and other unnecessary content while retaining files needed by both UI and .NET builds.
4. `docker build` and `docker run -p 8080:8080` work. The Compose file starts the same application using the published image and exposes port 8080 by default; allow straightforward override of the host port and image tag without editing the Compose file.
5. The image supports `linux/amd64` and `linux/arm64`. Both variants serve equivalent routes and frontend bytes for the same release.
6. Docker configuration does not embed secrets, require a private registry login for public releases, or introduce unrelated infrastructure.

### 3.4 Versioning and image releases

1. Container repository: `ghcr.io/montr/monoreport`.
2. Release source of truth is a Git tag following SemVer: `vMAJOR.MINOR.PATCH` for stable and `vMAJOR.MINOR.PATCH-prerelease` for prereleases (for example `v0.1.0-rc.1`). Reject invalid matching `v*` tags rather than silently assigning release metadata.
3. Stable release publishes the versioned tags plus `latest`; prerelease publishes versioned tags without changing `latest`. A branch push never publishes `latest`.
4. A release-built UI displays the version corresponding to the Git release tag; the backend/image and UI must not advertise unrelated release versions. For non-tagged local development/publish, use an explicit non-release/dev version fallback rather than presenting an unrelated published release. Implementation may use build-time metadata injection; do not require manual changes to `package.json` for each release.
5. GitHub Actions creates **no** Git tags or releases automatically. The Product Owner controls tagging and release decisions.
6. GHCR receives OCI metadata linking the image to the source repository. Public package visibility must be manually checked at first publication; workflow permissions alone do not guarantee public visibility.

### 3.5 GitHub Actions quality gates

1. Add initial workflow(s) under `.github/workflows/` for pull requests, pushes to `main`, and pushes of matching `v*` tags.
2. CI runs .NET restore/build/test (including existing test projects), frontend `npm ci`, TypeScript typecheck, frontend test suite, and production frontend compilation. Fail the workflow on any check failure.
3. Docker build runs on PRs and `main` **without publishing**. Start a built image and perform bounded HTTP smoke tests on the served UI, representative static resource, OpenAPI, at least one meaningful API request, backend-prefix 404, and missing-asset 404. The smoke checks must fail on unexpected HTML fallback, status, or content type.
4. Release-tag workflow may publish only after the equivalent automated quality gates and smoke checks succeed. It builds and publishes one multi-platform manifest for `linux/amd64` and `linux/arm64` via Buildx, using GHCR login from built-in `GITHUB_TOKEN` (`contents: read`, `packages: write`). No personal access token is required.
5. Explicitly verify that published manifest contains both architectures. Do not claim successful public pull until package visibility is confirmed.
6. Separate test jobs from the final runtime Docker image; use caching and job reuse where worthwhile, without duplicate publish/build work solely to satisfy a procedural checklist.
7. CI must not depend on browser automation or Microsoft Word for this Feature; browser visual acceptance is performed by the Product Owner. Provide useful diagnostics and bounded startup retries/timeouts for container checks.
8. PR validation must not be granted unnecessary GHCR write credentials; keep write permissions limited to the publishing job/event.

### 3.6 README and documentation

1. After implementation and verification, add a **small, factual section** to the existing README explaining development with HMR, local production `dotnet publish` and run, local Docker build/run, Compose from GHCR, GHCR pull/run, port configuration, and release tag behavior.
2. Commands must reflect actually verified entry points and operating assumptions. Do not restructure the entire README or rewrite product positioning.
3. Keep Feature documentation and evidence concise. Preserve unrelated history; do not reread/re-audit Feature 004 without a direct dependency.

## 4. Out of scope

1. Changing the existing developer Webpack/HMR topology or adding reverse proxying from ASP.NET Core to Webpack.
2. Kubernetes, hosting deployment, registries other than GHCR, external secrets, databases, volumes, and long-running stateful infrastructure.
3. New health endpoint, authentication, public API changes, UI redesign, or changes to DOCX generation semantics unless genuinely necessary for the approved scope.
4. General README reorganization, universal release automation, automated tag creation, and browser E2E infrastructure.
5. Mandatory multi-agent handoffs or repeated broad review for localized implementation changes.

## 5. Acceptance criteria

1. From a clean checkout with specified .NET SDK and Node runtime, release `dotnet publish` includes a working frontend; published API starts **without Docker** and serves `/`, real hashed assets, `/docs/`, `/openapi/v1.json`, and existing API operations from one origin.
2. Direct navigation to a frontend-style route returns SPA entry; unknown backend routes under all reserved prefixes and missing assets do **not** return SPA HTML. Documentation-disabled behavior is preserved.
3. The original development commands still provide Webpack HMR and functioning `/api`, `/docs`, and `/openapi` proxy routes.
4. Built local Docker image starts non-root on port 8080; its UI/API/docs, fallback rules, and representative DOCX API operation work. Runtime image excludes Node.js, npm, .NET SDK, and frontend sources.
5. `docker compose up -d` uses the GHCR image without building locally; configurable image version and host port work. At first release, public pull succeeds without credentials once package visibility is set to public.
6. CI shows passing .NET tests, frontend typecheck/tests/build, Docker build and container smoke tests on a PR/main run. Failures propagate and prevent a release.
7. A prerelease tag publishes accessible `amd64` and `arm64` manifest variants and tagged image without updating `latest`; a stable tag publishes both plus `latest`. Invalid `v*` release tags fail validation.
8. Published UI version matches stable/prerelease release metadata; untagged local builds do not masquerade as a published release.
9. The README documents verified development/non-Docker/Docker/Compose/GHCR paths without wholesale reorganization.
10. PO confirms final results and authorizes Feature closure; no staging, commits, pushes, tags, or GitHub settings changes are performed by Codex without explicit approval.

## 6. Implementation and verification approach

1. Use the agreed **single-Codex experimental workflow**: one agent prepares a compact implementation plan and implements the approved Feature. Avoid mandatory Architect/Reviewer role cycles; escalate any unexpected security, public-contract, binary-integrity or major architectural issue to the Product Owner before proceeding.
2. Codex may run appropriate local builds, tests and Docker smoke checks once implementation begins, where tooling is available. Browser sessions require separate PO approval. Do not infer CI/GHCR publication success from local checks.
3. PO owns commits, pushes, Git tags, GitHub settings and final acceptance. GitHub-hosted validation and actual GHCR publication must be verified after PO pushes changes/tags; record unexecuted remote checks as pending, not passed.
4. For the workflow experiment, record actual token telemetry if available and the number of PO correction cycles. Compare with Feature 004 cautiously: its 41.59M processed context tokens include cached/repeated input, not monetary cost; a small CI/hosting Feature is not an equal-complexity benchmark.

## 7. Known risks and mitigations

1. **SPA fallback intercepts backend errors:** explicitly exclude reserved backend prefixes and missing file paths; cover with HTTP smoke checks.
2. **Frontend build duplication/stale artifacts:** one documented publish contract, an intentional Docker skip-build property, and clean-build acceptance checks.
3. **Architecture build expense:** validate the local/CI smoke path first on the runner architecture; publish both architectures on release. Keep caching and time-budget controls; do not silently drop arm64 from release criteria.
4. **GHCR package visibility and first release:** user verifies package visibility and unauthenticated pull after first push; workflow reports only what it actually confirmed.
5. **Version mismatch:** derive release-visible version from the same Git tag used for image tags and test both stable and prerelease conventions.
6. **No existing release conventions or Actions:** implement minimal deterministic tag validation, avoid surprising release side effects.

## 8. Decisions reserved for Product Owner

1. Approval of this Feature Spec before implementation.
2. First actual Git release tag and timing of first public GHCR publication.
3. GitHub package visibility change (if needed) and final browser acceptance.
4. Changes to general `AGENTS.md`, Skills and project governance are excluded from this Feature pending separate decisions.
