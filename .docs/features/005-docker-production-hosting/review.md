# Feature 005 — Implementation Review and Verification

**Status:** Accepted and completed
**Date:** 2026-10-15

## 1. Review path and result

1. The Product Owner approved the single-Codex experimental workflow and explicitly waived independent Architect and Reviewer agent cycles for this Feature.
2. Codex reconciled the implementation against every acceptance criterion and inspected the highest-risk behavior: SPA fallback must never replace backend-prefix errors, disabled documentation routes, or missing file responses.
3. Automated host tests and real-host/container smoke checks cover those boundaries with status, content-type, and response-body assertions. No unresolved local implementation blocker remains.
4. On 2026-10-15, the Product Owner accepted the implementation after local, manual, GitHub-hosted, and public-image verification. Feature 005 is complete.

## 2. Implemented behavior

1. ASP.NET Core serves the production Playground, hashed static assets, API, Swagger, and OpenAPI from one origin, with SPA fallback limited to non-file frontend routes.
2. A Release `dotnet publish` cleans and builds the UI through locked npm dependencies and includes it under published `wwwroot`; normal build, test, and backend debugging do not invoke npm.
3. Docker uses separate Node, .NET SDK, and ASP.NET runtime stages. The final image runs as a non-root user on port 8080 and excludes Node.js, npm, the .NET SDK, test tooling, and frontend sources.
4. Compose defaults to `ghcr.io/montr/monoreport:latest` and supports image repository, image tag, and host-port overrides without editing the file.
5. GitHub Actions defines PR/main quality and container gates plus tag-only SemVer validation and multi-platform GHCR publication. Only the publishing job receives package-write permission.
6. Stable release metadata adds the exact version and `latest`; prerelease metadata adds only the exact version. The same normalized tag version is injected into the UI and OCI image metadata; local builds display `dev`.
7. README instructions were added only after the corresponding development, publish, Docker, and Compose entry points were exercised locally.

## 3. Executed verification evidence

1. `dotnet test MonoReport.Api.Tests/MonoReport.Api.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~StaticHostingTests` passed 8 tests covering UI/static content types, frontend fallback, reserved backend prefixes, missing assets, and documentation-disabled behavior.
2. `dotnet publish MonoReport.Api/MonoReport.Api.csproj -c Release --no-restore -o /private/tmp/feature005-final-publish` passed and executed `npm ci` plus the production webpack build before publishing UI assets under `wwwroot`.
3. The published application was started from its publish directory and checked over real Kestrel HTTP for `/`, a real hashed asset, `/docs/`, `/openapi/v1.json`, `/future/report/42`, sample DOCX validation, reserved-prefix 404 responses, missing-asset 404, content types, and SPA response identity. All checks passed.
4. `npm ci && npm run typecheck && npm test -- --run && npm run build` passed: typecheck succeeded, 19 tests passed across 5 files, and webpack completed successfully.
5. `dotnet build MonoReport.slnx -c Release --no-restore && dotnet test MonoReport.slnx -c Release --no-build --no-restore` passed with no build warnings or errors; 227 Core, 65 CLI, and 46 API tests passed.
6. `docker build --build-arg APP_VERSION=0.1.0-rc.1 -t monoreport:feature005 .` passed after the Product Owner increased Docker Desktop memory from 2 GiB to 8 GiB. The earlier two attempts ended during webpack with `cannot allocate memory`; no code or architecture change was needed.
7. `bash scripts/container-smoke.sh monoreport:feature005 8080` passed UI, hashed asset, Swagger, OpenAPI, sample DOCX validation, reserved-prefix and missing-asset 404, non-root user, and runtime-content assertions.
8. Compose was started with `MONOREPORT_IMAGE=monoreport`, `MONOREPORT_TAG=feature005`, and `MONOREPORT_PORT=8081`; the overridden image and port served the UI and OpenAPI successfully, after which the temporary service and network were removed.
9. The existing development topology was started with the API on port 5080 and Webpack Dev Server on port 3000. UI serving plus `/api`, `/docs`, and `/openapi` proxy behavior passed curl checks without browser automation.
10. `node .github/scripts/release-version.mjs` accepted `v0.1.0` and `v0.1.0-rc.1`, classified stable and prerelease correctly, and rejected invalid `v01.2.3`. `docker compose config` and `git diff --check` passed.

## 4. External verification and accepted residual risks

1. The Product Owner reported successful GitHub Actions validation after publishing the repository changes.
2. The Product Owner verified successful publication and public availability of `ghcr.io/montr/monoreport:0.1.0` with both `linux/amd64` and `linux/arm64` manifest variants.
3. The Product Owner manually verified that the production UI runs correctly in Docker, including Monaco Editor and the Format action, and also verified the development mode. No browser automation was run by Codex.
4. The completed npm audit reported 9 vulnerabilities. The 5 high-severity findings are confined to development dependencies. The production dependency tree retains 2 low-severity DOMPurify findings through Monaco Editor; these residual risks were previously reviewed and accepted in Feature 004. No dependency versions were changed for Feature 005.
5. Local Docker builds require sufficient Docker Desktop memory for the webpack stage. Two attempts at a 2 GiB limit failed with resource exhaustion; the build succeeded at 8 GiB. This local resource requirement does not change the produced runtime image.

## 5. Workflow telemetry

1. Actual Codex token telemetry was not available and is not estimated.
2. Product Owner correction cycles: 0.
3. Product Owner infrastructure interventions: 1, increasing Docker Desktop memory after reproducible local resource exhaustion.

## 6. Product Owner decision

1. On 2026-10-15, the Product Owner explicitly accepted Feature 005 and directed the Orchestrator to close it according to the project workflow.
2. Feature 005 is completed. This acceptance and closure did not authorize or cause Codex to stage files, create commits, push, or create tags.
