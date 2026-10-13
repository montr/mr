# Feature 004 — Product Owner Manual Acceptance

Date: 2026-10-14
Status: **Functional manual checks PASS (6/6); feature acceptance pending**

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
