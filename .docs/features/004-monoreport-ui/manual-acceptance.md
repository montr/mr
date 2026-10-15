# Feature 004 — Product Owner Manual Acceptance

Date: 2026-10-14; final acceptance recorded 2026-10-15
Status: **ACCEPTED — Feature 004 completed**

## Environment

1. Browser: Google Chrome 155.0.8059.40 (Official Build, arm64) on macOS.
2. Office application: Microsoft Word on macOS.
3. Application: locally running `MonoReport.UI` (`http://localhost:3000`) and `MonoReport.Api`.

## Manual verification results

1. **PASS — Live HTTP 422 through UI.** A request with `{"Materials":{}}` returned HTTP 422. The Errors tab showed one error: `Document generation failed`; `generation.invalid_data · Materials · The data is incompatible with the template.`
2. **PASS — Downloaded DOCX opens in a desktop office application.** The document downloaded from the UI opened in Microsoft Word on macOS, with populated fields and table content.
3. **PASS — Monaco Format.** The Product Owner manually verified the Format action.
4. **PASS — Invalid JSON.** The Product Owner verified invalid-JSON handling. Errors displayed one item: `Invalid JSON`; `Fix the JSON syntax before generating a document.`
5. **PASS — Unavailable API.** The UI displayed one error: `Request failed`; `The server returned HTTP 504.` The Product Owner accepted this behavior for the manual check. The 504 may be produced by the development proxy; that origin was not independently verified.
6. **PASS — Pointer splitter resizing.** The Product Owner manually verified pointer-based resizing.

**Manual checklist outcome: 6/6 PASS.** These are Product Owner-reported results; no new automated browser checks are implied.

## Outstanding product and visual findings

1. **Preview content discrepancy — requires investigation before final feature acceptance.** In both Template and Result views, document fields and table values appeared blank in the browser preview, whereas the downloaded generated DOCX displayed populated values correctly in Microsoft Word. Template MERGEFIELD placeholders may explain some blank template values, but the blank generated Result preview remains a substantive issue. Investigate `docx-preview` rendering and/or generated OOXML compatibility; do not assume the cause.
2. **Monaco layout:** a short unintended horizontal line/visual artifact appears near the right edge of the editor's first line.
3. **Errors empty-state alignment:** the empty-state icon/content appeared incorrectly positioned.
4. **Tabs focus styling:** a conspicuous red rectangular outline appeared around the focused Errors tab; distinguish active underline from keyboard-only focus styling.
5. **Visual parity against approved v10 mockup:** further Product Owner feedback is pending. Do not treat this list as exhaustive.

## Acceptance and workflow

1. The six previously outstanding *manual functional* checks are complete. This record does **not** constitute final acceptance of Feature 004.
2. Preserve earlier automated, API, download-integrity, and browser evidence in `review.md`. Do not repeat unaffected verification solely to record these PO results.
3. Further UI observations and prioritization will be provided separately by the Product Owner. Address preview and visual findings through the established review/approval workflow.
4. No additional headless Chrome/CDP sessions are authorized. Browser-based visual and interactive acceptance should normally be performed manually by the Product Owner; automated browser sessions require explicit, separately justified authorization. Discuss codifying this rule in the Feature 004 retrospective.
5. Git staging and commits remain solely under Product Owner control.

## Final Product Owner Acceptance — 2026-10-15

1. The Product Owner explicitly accepts Feature 004, including the complete implementation, verified functional behavior, and all subsequently approved corrections. This final decision supersedes the pending acceptance state above without altering the historical observations, review findings, or evidence that led to it.
2. The accepted baseline includes browser-side `w:fldSimple` normalization for both Template and Result preview copies. The original selected template remains authoritative for API upload, and the original API response remains authoritative for Download.
3. The accepted presentation baseline includes centered Errors empty state, a visible mobile Download icon and label, the approved footer icons, and the final Monaco layout and typography with a 12-pixel font and enabled minimap.
4. The accepted local-development baseline includes Webpack proxy routes for `/api`, `/docs`, and `/openapi`.
5. The Product Owner confirms manual verification of the final functionality and presentation, including the corrected Template and Result previews and final Monaco presentation. The earlier six-item functional checklist remains PASS.
6. Previously documented residual risks are accepted, including the approximately 13.2 MiB bundle, bounded DOCX preview-normalization and renderer-fidelity risks, and Monaco 0.57.0's two low-severity advisories involving vendored DOMPurify 3.4.15. The sanitizer risk should be revisited when a compatible upstream Monaco fix becomes available.
7. The independent reviews, prior `CHANGES REQUESTED` verdicts, investigation results, limitations, and correction evidence remain preserved as audit history. The later scoped DOCX preview implementation received independent PASS; subsequent localized presentation and proxy corrections are accepted by the Product Owner as part of the final baseline.
8. The approved Feature Spec, retained implementation evidence, Product Owner manual results, and accepted follow-up changes are consistent with the completed status. Feature 004 is therefore closed with `feature-status.json` set to `completed`.
