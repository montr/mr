# Feature 005 — Production Hosting Retrospective

Date: 2026-10-15
Status: Completed and submitted for Product Owner discussion; recommendations not adopted

## 1. Evidence and comparison limits

1. This retrospective uses the approved Feature 004 and 005 specifications, Feature 004 retrospective, Feature 005 plan and review evidence, recorded Product Owner decisions, and available Codex telemetry. It does not re-audit implementation or change prior verdicts.
2. Feature 004 recorded 41,588,618 processed context tokens, including cached and repeatedly supplied input, and approximately 156,650 output tokens. Its observable work included roughly an 82-minute initial window plus separate correction windows; this is not reliable active-labor time.
3. Exact Feature 005 token totals, model-output totals, active time, GitHub Actions duration, and release-job duration are unavailable. They are not estimated. The two Features also differ materially in scope, so raw totals would not be a controlled cost comparison.

## 2. Single-agent workflow compared with Feature 004

1. Feature 005 eliminated Architect, Developer, Plan Reviewer, resumed Developer, and Implementation Reviewer handoffs. This removed the dominant Feature 004 costs identified in its retrospective: repeated full-context reads, append-only handoff duplication, one usage-limit recovery, and broad re-review for narrow corrections.
2. Continuity was visibly better. One agent retained the publish, hosting, Docker, workflow, documentation, and verification rationale; corrections to the MSBuild publish manifest and container smoke script were made within the same context instead of requiring role reconstruction.
3. Quality evidence remained strong for the approved scope: focused hosting tests, full .NET and frontend suites, real published-host checks, Docker runtime assertions, Compose overrides, development proxy checks, successful GitHub Actions, a public two-architecture image, and Product Owner checks of production UI, Monaco, Format, and development mode.
4. Feature 005 recorded zero Product Owner correction cycles. Feature 004 required multiple correction windows after review and manual acceptance exposed preview, Monaco layout, responsive, icon, proxy, and documentation issues. This favors the Feature 005 workflow operationally, but does not prove that self-review is generally equivalent: Feature 005 had fewer subjective UI and third-party rendering risks.
5. Product Owner participation was lower in correction volume but still essential at the boundaries an agent could not own: approval of the experimental workflow and plan, increasing Docker Desktop memory, Git operations, observing GitHub-hosted execution, publishing and confirming the public GHCR image, manual UI verification, residual-risk acceptance, and final closure.
6. Token efficiency was almost certainly improved because no multi-agent context duplication occurred, but the magnitude is unknown without Feature 005 telemetry. The defensible result is qualitative, not a numeric savings claim.

## 3. What reduced cost and what remained expensive

1. The largest savings came from one compact approved plan, a single implementation context, risk-focused automated checks, reuse of one container smoke script locally and in CI, delta-based evidence, and deferring README text until commands had actually been exercised.
2. Explicitly separating local evidence from remote evidence avoided pretending that GHCR, package visibility, or multi-platform publication had been verified before the Product Owner pushed and tagged. This reduced speculative investigation while preserving the approval boundary.
3. The expensive work was intrinsic integration work rather than governance: repeated production frontend compilation, clean npm installation, .NET publish, container layers, real-host startup, and multi-stage image construction. Monaco makes the frontend bundle and webpack stage memory-intensive.
4. Some avoidable cost remained. The publish manifest initially duplicated UI assets, the first smoke assertion expected the wrong validation property name, a root lockfile was briefly created from the wrong working directory, and staged-document whitespace required a later working-tree correction. These were localized self-review misses, although none escaped final verification.

## 4. Docker, CI/CD, and GHCR findings

1. Docker Desktop at 2 GiB reproducibly killed webpack with `cannot allocate memory`. After the Product Owner increased the limit to 8 GiB, the same Dockerfile completed without architectural changes. The successful first full build was still dominated by downloading the large .NET SDK layer; available BuildKit output showed that layer taking more than 13 minutes. Subsequent cached builds should be materially cheaper, but no benchmark was recorded.
2. The local runtime checks were high-value because they verified properties ordinary unit tests could not: non-root execution, port 8080, actual content types and fallback boundaries, representative DOCX validation, and absence of Node.js, npm, .NET SDK, and frontend sources.
3. GitHub Actions ultimately passed and the public `ghcr.io/montr/monoreport:0.1.0` manifest contains `linux/amd64` and `linux/arm64`. The release uses QEMU and Buildx for cross-platform publication. The actual QEMU overhead and total publication duration are unknown because those timings were not persisted in Feature evidence.
4. ARM64/QEMU remained the most expensive remote uncertainty until publication. Local testing covered the runner-native image; the manifest check proved both variants were published, while equivalent runtime bytes and behavior across both remote architectures were delegated to the deterministic Docker build and release workflow rather than separately exercised on two physical architectures.
5. GHCR required Product Owner-controlled tagging, workflow execution, and public-visibility confirmation. Keeping package-write permission only on the tag publishing job limited credential exposure, but this necessarily kept the final release path outside local agent verification.

## 5. Risks from omitting an independent Reviewer

1. The main residual process risk is confirmation bias across coupled configuration: the same agent designed the MSBuild target, Dockerfile, smoke script, workflow, and evidence interpretation. A shared mistaken assumption could therefore survive both implementation and self-verification.
2. CI/CD configuration had no independent semantic review before the first hosted run. Successful Actions and public publication substantially reduce this risk for version `0.1.0`, but they do not independently assess maintainability, least-privilege regressions, cache behavior, or future tag edge cases.
3. The manifest proves both architectures exist, not that each was separately smoke-tested after publication. QEMU build fidelity, architecture-specific runtime defects, and byte equivalence remain lower-probability residual risks.
4. Dependency risk remains explicitly accepted rather than eliminated: npm reported 9 vulnerabilities, with 5 high findings confined to development dependencies and 2 low production findings in DOMPurify through Monaco previously accepted in Feature 004.
5. Compared with Feature 004, no independent Reviewer found a product defect in Feature 005 because none was assigned. The absence of PO correction cycles and successful release are positive outcome evidence, but cannot measure defects that a reviewer might have prevented before verification.

## 6. Three improvements for future Features

1. **Use conditional independent review at declared risk boundaries.** Keep one-agent continuity by default, but require a focused reviewer only for public contracts, security or permission changes, release workflows, persistence, concurrency, or binary-integrity boundaries. Review only the affected files and acceptance mapping.
2. **Record lightweight comparable telemetry from the start.** Capture feature start/end decision timestamps, processed and output tokens when available, PO correction cycles, infrastructure interventions, and CI job durations in one evidence table. Mark unavailable values unknown rather than reconstructing logs later.
3. **Add a preflight and hosted-release evidence checklist.** Before expensive Docker work, verify working directory, publish item uniqueness, smoke response schema, available container memory, and required base images. After release, persist quality, native smoke, QEMU multi-arch duration, manifest platforms, and public-pull result. This targets the actual expensive failures without adding a broad role cycle.

## 7. Discussion conclusion

1. Feature 005 supports the hypothesis that a single-agent workflow can materially reduce handoff and context costs while maintaining quality for a bounded, testable infrastructure Feature with strong automated checks and active Product Owner ownership of release boundaries.
2. It does not justify removing independent review universally. The better next step is conditional, narrowly scoped review based on risk, combined with comparable telemetry over several Features before changing project governance.
