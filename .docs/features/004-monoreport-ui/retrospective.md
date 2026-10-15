# Feature 004 — MonoReport UI Retrospective

Date: 2026-10-15  
Status: Completed and submitted for Product Owner consideration; proposals not approved

## 1. Scope and Evidence Basis

1. This retrospective is analysis only. It does not change production code, tests, configuration, README, workflow rules, feature status, or prior review verdicts.
2. Evidence reviewed: `AGENTS.md`; the Orchestrator, Architect, Developer, and Reviewer Skills; Feature 004's approved Feature Spec, implementation plan and amendment history, implementation reviews, manual-acceptance record, DOCX compatibility investigation, approved v10 mockup, feature status, current source/configuration, Git history and working state, and available Codex rollout logs.
3. Confirmed facts below are supported by those artifacts, current source/history, or explicit Product Owner reports. Root-cause explanations marked as inference are not treated as proven.
4. No tests, builds, package operations, servers, browsers, CDP controllers, or new agents were used for this retrospective. Existing evidence was reused.
5. The Git index was inspected read-only and remains Product Owner-owned. At retrospective time it contains three staged follow-up files; this document is a new unstaged documentation file.

## 2. Outcome and Confirmed Verification

1. Feature 004 delivered a locally runnable static React and TypeScript playground using Ant Design, Webpack/SWC, Monaco, `docx-preview`, and Vitest with React Testing Library.
2. The UI implements DOCX selection and drag/drop, one preserved JSON document, disabled Form mode, Copy and Format, explicit Generate, Template/Result/Errors views, responsive and keyboard-operable splitters, API diagnostics, result download, and repository/version/API-documentation footer metadata.
3. Generation uses the existing `POST /api/documents/generate` multipart contract. The selected original `File` is uploaded and the original successful response `Blob` remains authoritative for Result state and Download. Editing JSON, replacing the accepted template, starting generation, or failing generation invalidates the prior result; only a new success restores it.
4. Existing automated evidence records focused frontend tests passing, TypeScript checks passing, and production Webpack builds passing. Existing API evidence records live HTTP 200 and 422 behavior, valid DOCX ZIP structure, and CLI validation. These checks were not repeated.
5. Product Owner normal-browser verification recorded 6/6 functional checks passing: live HTTP 422 presentation, downloaded DOCX opening with populated content in Microsoft Word on macOS, Monaco Format, invalid-JSON diagnostics and request blocking, unavailable-API error presentation, and pointer splitter resizing.
6. A preview-only compatibility layer now unwraps paragraph-level `w:fldSimple` cached children in supported DOCX story parts before `docx-preview` renders a private copy. It does not synthesize content or alter upload/download bytes. Focused normalization, byte-integrity, and stale-lifecycle tests passed; independent scoped implementation review returned PASS.
7. The Product Owner subsequently confirmed that both Template and Result previews display the expected content in a real browser. This resolves the reported simple-field preview defect for the tested documents without claiming universal `docx-preview` fidelity.
8. The Product Owner also reports successful final manual verification of the Monaco layout, 12-pixel font size, and enabled minimap. Current staged source contains those settings plus the flex/overflow containment correction. This is PO-reported visual evidence, not an automated layout assertion.
9. The Errors empty composition, mobile Download label, and footer API-documentation icon were corrected in localized UI changes. The development proxy was subsequently expanded from `/api` alone to `/api`, `/docs`, and `/openapi`, allowing the footer documentation route and OpenAPI assets to reach the API during local development.
10. At the time of retrospective analysis, Feature 004 remained in review. The Product Owner subsequently accepted the complete baseline on 2026-10-15, and `.docs/features/004-monoreport-ui/feature-status.json` was advanced to `completed` as a separate closure action.

## 3. Problems Found and Root Causes

1. **DOCX preview omitted fields.** The tested documents contained paragraph-level `w:fldSimple` cached content that `docx-preview` 0.4.1 did not preserve. Production generation and downloaded DOCX bytes were correct; only browser preview was wrong. The isolated private-copy normalizer fixed the confirmed simple-field case. An observed interaction with neighboring complex table fields remains incompletely explained and is documented separately rather than generalized.
2. **Monaco height and scrollbar artifact.** The editor flex item retained a minimum height and its parent did not contain overflow, so the Monaco host could exceed the space remaining beneath fixed toolbars. This also produced the apparent boundary scrollbar artifact. Making the source/editor flex chain shrinkable and explicitly containing width, height, and overflow addressed the layout cause. Typography and minimap were then adjusted to the PO-approved final presentation.
3. **Errors empty state was not centered.** The full-width `errors-view` alignment intended for populated diagnostics was also applied to the empty state. Conditioning that class on actual errors preserved full-width diagnostics while allowing the complete “No Errors” composition to center.
4. **Mobile Download lost its label.** A narrow-screen CSS rule explicitly hid the text adjacent to the Download icon. Removing that rule and tightening existing spacing restored the approved icon-plus-label treatment without hiding another control.
5. **Footer icons diverged from the approved mockup.** The initial implementation omitted the GitHub icon, and a later comparison identified the missing API-documentation icon. Both were literal mockup-parity misses, corrected without changing destinations or labels.
6. **Development proxy routes were incomplete.** Webpack initially proxied only `/api`; `/docs/` and `/openapi` were same-origin UI routes rather than API routes in development. Git commit `8bdf832` added both contexts. This was a local-development integration omission, not an API contract change.
7. **Responsive splitter state initially diverged from CSS.** Independent review found that orientation changes could leave accessible values and CSS sizing out of synchronization. The correction added focused breakpoint-transition and accessibility coverage.
8. **Production dependency advisories.** Monaco 0.57.0 includes vendored DOMPurify 3.4.15 affected by two low-severity advisories. Investigation showed that an npm override would not remove bundled code and npm's proposed remediation was an unapproved downgrade. The PO accepted the documented residual risk until a compatible upstream Monaco fix is available.

## 4. Why Tests and Earlier Reviews Missed the Defects

1. Vitest/jsdom tests deliberately mocked Monaco and `docx-preview`. They were effective for application-owned state, object identity, multipart transport, stale-operation protection, warnings, and diagnostics, but could not exercise third-party rendering fidelity, CSS geometry, real scrollbars, or office-document opening.
2. The initial browser evidence emphasized the shell and later the primary select-preview-generate-preview-download path. Early sessions did not execute all interaction criteria, and synthetic Monaco manipulation failed to establish several checks. The normal-browser PO pass was the first complete functional pass through the actual editor and desktop office application.
3. The approved mockup was treated as a visual reference but was not converted into a compact structural checklist. Simple omissions such as footer icons and the mobile Download label therefore survived component tests and initial review.
4. Review focused appropriately on higher-risk state and API boundaries. However, broad artifact review is a poor substitute for a short visual parity pass: it caught the splitter state defect and GitHub icon, but missed or deferred the DOCX renderer behavior, Monaco geometry, Errors centering, documentation icon, and proxy route integration.
5. The preview tests asserted which Blob reached a mocked renderer, not whether the real renderer retained OOXML field content. A minimal real-library fixture or an explicit early manual preview smoke check would have exposed the incompatibility before the main acceptance phase.
6. CSS behavior at real dimensions cannot be established credibly by jsdom. Source assertions could have caught the explicit mobile label-hiding rule and missing icon markup cheaply, but editor height, overflow, and pointer behavior still required a real layout engine and human inspection.

## 5. Workflow Efficiency Findings

### 5.1 Available Usage Telemetry

1. Codex rollout logs provide exact per-turn input/output token counters, including cached input. They do not provide billing cost, reliable active-labor time, or a native feature-phase label. Counts below exclude this retrospective and should be read as context-processing volume, not cost.
2. Total recorded Feature 004 volume through the final Monaco correction was **41,588,618 tokens**. Approximately **156,650** were model output tokens; most of the remainder was repeatedly supplied or cached context. Full-history handoffs and growing append-only artifacts dominate this number.
3. By role/session: Orchestrator **12,636,230**; Architect **1,925,657**; Developer for initial plan/implementation **9,889,633**; Plan Reviewer **1,358,605**; resumed Developer **12,887,785**; Implementation Reviewer **2,890,708**.
4. Reconstructed phase attribution is not native telemetry: specification review **1,009,057**; initial planning and plan review **2,842,820**; initial implementation, correction, browser work, and implementation review **23,519,559**; DOCX follow-up investigation and planning **4,929,276**; scoped preview implementation and review **7,664,455**; direct presentation and Monaco fixes **1,623,451**.
5. Session timestamps include Product Owner decision gaps and idle agents, so active work duration cannot be measured honestly. Observable interaction was concentrated in roughly an 82-minute window on 2026-10-13, followed by separate correction windows on 2026-10-14 and 2026-10-15; those wall-clock windows are not labor estimates.

### 5.2 Duplication and Context Loss

1. Each independent role repeatedly reread the Feature Spec, long implementation plan, accumulated review, mockup, repository rules, source, and diffs. This improved auditability but made narrow corrections inherit the cost of the entire feature history.
2. The implementation plan and review grew by appending every verdict, correction, evidence set, and handoff. Preserving history was valuable; duplicating current conclusions and evidence in several documents was not.
3. One Developer run reached its usage limit, requiring a resumed Developer session to reconstruct state. A later interrupted handoff caused another context reload. The filesystem preserved code, but rationale and exact evidence applicability had to be rediscovered.
4. The one-line `package-lock.json` wording correction required a Developer amendment and full independent re-review. It enforced an important Git boundary, but the process cost was disproportionate to the defect.
5. Review uncertainty about staged files persisted until the PO clarified that the index was intentionally prepared for review. A standard handoff field identifying index ownership would have avoided repeated investigation.

### 5.3 Browser and Review Value

1. At least six headless Chrome sessions are evidenced: four launches noted by the PO, a later bounded attempt whose controller returned non-serializable DOM nodes, and one additional bounded session that verified the primary workflow. They produced useful shell, responsive, download, warning, and primary-flow evidence, but did not complete Monaco-driven checks or desktop-office validation.
2. The most valuable browser activity was the single bounded primary-flow run. The highest-value acceptance activity overall was the PO's normal-browser and Word pass: it completed all six remaining functional checks and exposed the DOCX-preview and presentation defects that mocks could not reveal.
3. The failed controller attempt was avoidable. Its DOM-evaluation contract should have required scalar or small serializable results before Chrome launch. Repeated exploratory launches had diminishing value once the remaining criteria clearly depended on normal editor interaction and a desktop office application.
4. Independent review prevented real defects: responsive splitter/CSS-accessibility synchronization, missing GitHub icon, incomplete acceptance evidence, and ineffective dependency-remediation assumptions. It also preserved the critical upload/download byte boundary during preview normalization.
5. Some findings were mainly procedural: the plan's instruction to “commit” the lockfile, status/index bookkeeping, and repeated restatement of already known manual gaps. These could have been detected by a cheap policy lint/checklist or resolved inline without a broad review cycle.

## 6. Most and Least Valuable Activities

1. **Most valuable:** PO verification in a normal browser and Microsoft Word; focused inspection of real OOXML and installed `docx-preview`; object-identity and stale-operation tests around authoritative bytes; direct API/DOCX integrity evidence; independent review of splitter accessibility and preview isolation.
2. **Valuable but bounded:** production dependency investigation. It ruled out an ineffective override and enabled an informed residual-risk decision, but did not justify repeated audit runs or unsupported patching.
3. **Least valuable:** repeated full-document rereads for small deltas; multiple broad headless retries after browser limitations were known; full re-review for a one-line procedural wording fix; duplicated evidence prose across plan, review, and handoff; leaving idle subagents available across long PO decision gaps.

## 7. Alternative Workflow Comparison

1. **Current full multi-agent workflow.** Highest auditability and strongest separation of duties, but highest token volume, latency, context duplication, and PO gate count. It is appropriate for public contracts, security/data boundaries, irreversible migrations, or architecture with several viable choices. Feature 004 benefited at its byte-integrity and responsive-state boundaries, but not every CSS correction warranted the same machinery.
2. **One Codex agent for planning, implementation, and self-verification.** Lowest latency and context cost, with fast correction loops and little handoff loss. It carries the greatest confirmation-bias and omission risk. It is suitable for documentation and small reversible presentation fixes when the PO can perform the visual check.
3. **One implementation agent with conditional independent review.** Moderate cost and latency, good continuity, and independent scrutiny where risk warrants it. This is the recommended default: require a reviewer for contract, security, concurrency, dependency, binary-integrity, or cross-component changes; allow direct work plus targeted verification for localized CSS/copy/docs changes.
4. **Recommended risk model.** Class A: documentation/copy/local presentation—single agent and PO visual acceptance. Class B: bounded client behavior or third-party adapter—single implementer plus targeted independent implementation review. Class C: public API, persistence, security, concurrency, or irreversible change—full Architect/Developer/Reviewer workflow with both approval gates.
5. A suitable controlled experiment is a small future “copy one error diagnostic” UI feature: no API or document-format change, reversible, behavior-testable, and manually observable. Use one implementation agent, a predeclared risk checklist, conditional review only if scope crosses a boundary, and compare tokens, elapsed decision time, reopened defects, and PO correction count with Feature 004. Do not run the experiment as part of this retrospective.

## 8. Prioritized Process Improvements

### 8.1 Quick Wins

1. **Add an intake risk classification and workflow route.** Benefit: avoids applying maximum ceremony to low-risk work. Downside: initial classifications can underestimate integration risk. Measure: tokens and elapsed time per risk class, escaped defects, and number of escalations to a stricter route.
2. **Create one live acceptance/evidence matrix with criterion, owner, evidence link, and status.** Benefit: prevents repeated prose and reopening resolved checks. Downside: another artifact must be maintained. Measure: duplicate evidence paragraphs, reviewer clarification requests, and acceptance gaps discovered late.
3. **Require delta-based handoffs.** A handoff should name changed files, decisions since the last gate, applicable prior evidence, invalidated evidence, index ownership, and exact requested review scope. Benefit: reduces rereading and context loss. Downside: a poor delta can omit context. Measure: input tokens per correction review and time spent reconstructing state.
4. **Introduce a mockup parity checklist before first review.** Include required labels/icons, narrow-width visibility, empty/populated states, footer destinations, and focus treatment. Benefit: cheaply catches literal UI omissions. Downside: does not prove geometry or aesthetics. Measure: count of PO-found parity defects after review.
5. **Set browser launch prerequisites and a launch budget.** Every launch must name the unverified criterion, controller return schema, fixture, and stopping condition; visual/office checks default to a consolidated PO manual pass. Benefit: fewer retries and clearer evidence. Downside: less exploratory discovery. Measure: successful criteria per launch, failed-controller launches, and browser time per accepted criterion.
6. **Separate review findings into product defects, acceptance-evidence gaps, residual risks, and process violations.** Benefit: makes severity and required action explicit. Downside: slightly more reviewer discipline. Measure: PO clarification rounds and findings later reclassified.
7. **Use scoped re-review by default after correction.** Reinspect the changed boundary and explicitly reused evidence rather than repeating a full audit. Benefit: lower latency while preserving independence. Downside: cross-cutting effects can be missed. Measure: re-review token volume and regressions outside declared scope.
8. **Add an acceptance-time stale-record check.** Compare feature status, backlog status text, README commands, dependency-risk records, and current approved decisions once near acceptance. Benefit: finds stale documentation without repeated audits. Downside: adds one final checklist. Measure: stale items found after feature closure. Feature 004 currently illustrates the need: its status correctly remains `review`, while the backlog still describes HTTP API as “Selected for Feature 003” even though Feature 003 is complete.

### 8.2 Experimental Changes Requiring PO Approval

1. **Adopt the Class A/B/C workflow formally.** Benefit: substantial token and latency reduction while preserving review for meaningful risk. Downside: changes the current uniform governance model. Measure over three features: tokens, cycle time, PO interventions, escaped defects, and audit completeness.
2. **Permit inline correction of non-substantive plan defects without a new role cycle.** Benefit: avoids a full re-review for wording such as the lockfile instruction. Downside: disagreement over what is substantive. Measure: number of inline corrections later found to alter scope or architecture; any such event should tighten the rule.
3. **Use compact decision/evidence ledgers and archive superseded narrative sections.** Benefit: reduces context size while preserving traceability. Downside: links and archival structure require discipline. Measure: artifact size, repeated-reading tokens, and ability of a fresh reviewer to reconstruct the decision trail.
4. **Add a real-library contract test tier for third-party renderers without adding browser E2E infrastructure.** Benefit: can catch parser omissions using representative packages before visual acceptance. Downside: renderer DOM output may be brittle and still cannot replace visual inspection. Measure: third-party integration defects caught before PO testing versus test maintenance cost.

## 9. Remaining Formal Acceptance Gaps and PO Decisions

1. The Product Owner supplied explicit final acceptance on 2026-10-15 for the complete implementation and all approved follow-up changes, including the DOCX preview correction, localized presentation fixes, proxy routes, final Monaco presentation, and previously documented residual risks.
2. The final acceptance record resolves the earlier evidence and visual-parity gaps. Feature 004 is now `completed`; no resolved defect is reopened by this retrospective.
3. The staged Monaco and DOCX investigation follow-up remains PO-owned and is explicitly included in the accepted baseline. This retrospective and closure action do not stage, unstage, commit, or reinterpret those changes.
4. The retrospective itself is complete and submitted for Product Owner consideration. None of its process-improvement proposals is approved merely by accepting Feature 004.
5. Separate PO approval remains required before changing `AGENTS.md`, Skills, workflow rules, backlog wording, or acceptance procedures based on the proposals above.

## 10. Retrospective Conclusion

1. Feature 004 achieved its functional product goal and ultimately obtained strong evidence across automated tests, live API/document checks, independent review, PO normal-browser testing, and Microsoft Word. The main escaped defects were integration and presentation defects at boundaries intentionally mocked by unit tests.
2. The full multi-agent workflow added real value at high-risk boundaries, but its uniform application, repeated full-context handoffs, append-only evidence duplication, and repeated browser attempts made the feature substantially more expensive than necessary.
3. The recommended change is not to remove independent review. It is to apply review and browser evidence according to risk, carry forward a single explicit evidence matrix, and reserve broad re-audits for changes that can invalidate broad evidence.
4. The retrospective is completed and submitted for Product Owner consideration. Feature 004 was accepted and closed by the Product Owner's separate explicit decision, not by this retrospective; its process-improvement proposals remain unapproved pending separate decisions.
