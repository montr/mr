# DOCX field compatibility in browser preview

**MonoReport / Feature 004 — investigation, 2026-10-14**

## 1. Summary

1. `docx-preview` 0.4.1 can omit visible MERGEFIELD content from valid DOCX files. The issue concerns the **browser preview**, not the validity of the DOCX or necessarily MonoReport.Core generation.
2. MonoReport.UI uses a **browser-only normalization** before `docx-preview`: in a private DOCX copy, it unwraps supported paragraph-level `w:fldSimple` elements while preserving their existing cached child runs. The original template sent to the API and the original generated file offered for download are unchanged.
3. The workaround is intentionally isolated in `MonoReport.UI/src/services/docxPreviewNormalization.ts` and its invocation in `DocxPreview.tsx`. Remove it only after a tested upstream `docx-preview` release correctly renders the regression documents.

## 2. Reproduction materials

1. `Samples/sample.template.docx` — sample template created programmatically (the original uploaded comparison file).
2. **Word-resaved comparison (not stored in Git):** to reproduce, open `Samples/sample.template.docx` in Microsoft Word, add two fields near the beginning of the document (one intentionally invalid field, and one valid `MERGEFIELD Test`), then use **Save As** to create a separate DOCX. In the original experiment, Word displayed `Error! No bookmark name given.` for the invalid field. The exact OOXML emitted by Word may vary with its version and editing steps; the counts below describe the actual 2026-10-14 comparison, not guaranteed output of every reproduction.
3. `Samples/sample.json` — sample input with three `Materials` rows.
4. PO browser comparison on 2026-10-14: Template and Result, each with normalization enabled and disabled, for both templates. Screenshots were examined in conversation but are intentionally **not committed to the repository**.

## 3. Verified OOXML differences

Counts from `word/document.xml` using namespace-aware XML inspection:

| Element | Programmatic template | Word-resaved copy (original experiment) |
| --- | ---: | ---: |
| `w:fldSimple` | 21 | 1 |
| `w:fldChar` | 0 | 66 |
| `w:instrText` | 0 | 22 |

1. The original has 21 simple MERGEFIELDs, each represented as a paragraph child `w:fldSimple` with a `w:instr` instruction and cached `w:r/w:t` content (for example `«Project.Name»`).
2. The Word-resaved copy converted 20 original fields to complex fields (`w:fldChar` begin/separate/end and `w:instrText`); the original `MERGEFIELD Unit` remained a `w:fldSimple` with cached `«Unit»`.
3. Its 22 `w:instrText` elements represent 20 converted original fields plus two inserted fields. The first added field has an incomplete `MERGEFIELD` instruction and Word shows the bookmark-name error; the second is `MERGEFIELD Test`.
4. Both representations are valid OOXML field representations. Therefore **a simple field is not a malformed template**, and it is incorrect to reject one merely because a particular browser renderer fails to show it.

Simplified forms (illustrative, not verbatim document XML):

```xml
<!-- Simple field: cached visible result is inside the wrapper. -->
<w:fldSimple w:instr=" MERGEFIELD Project.Name ">
  <w:r><w:t>«Project.Name»</w:t></w:r>
</w:fldSimple>

<!-- Complex field: instruction and cached result use separate runs. -->
<w:r><w:fldChar w:fldCharType="begin"/></w:r>
<w:r><w:instrText xml:space="preserve"> MERGEFIELD Project.Name </w:instrText></w:r>
<w:r><w:fldChar w:fldCharType="separate"/></w:r>
<w:r><w:t>«Project.Name»</w:t></w:r>
<w:r><w:fldChar w:fldCharType="end"/></w:r>
```

## 4. Actual browser observations

| Document | Without normalization | With normalization |
| --- | --- | --- |
| Programmatic sample — Template | Merge-field labels missing throughout | Cached labels and collection markers visible |
| Programmatic sample — Result | Scalar cells blank; table has generated row count but values missing | Scalar data and all three material rows visible |
| Word-resaved copy — Template | Many labels visible, but some table fields (e.g. `Reference`, `Description`, `Currency`) missing | Field labels visible throughout the sample |
| Word-resaved copy — Result | Some fields populated, but corresponding table values missing | Scalar data and all three rows visibly populated |

The screenshots establish these **visual outcomes**, not the exact library-code branch responsible for every missing complex-field value. In particular, the remaining original `Unit` simple field does **not** by itself explain all empty cells observed in the Word-resaved copy. A deeper renderer trace would be needed to attribute those additional gaps definitively.

The successful generation from the Word-resaved copy, including repeated rows and scalar values visible with the workaround, gives practical evidence that Core processes the complex fields in this sample. It does not prove every OOXML field shape is supported by Core.

### Follow-up: complex table fields that also disappeared (2026-10-15)

A targeted OOXML inspection of the Word-resaved comparison established the following details in `word/document.xml`:

1. The sole remaining `w:fldSimple` is `MERGEFIELD Unit`, located in a material-row table cell and containing cached text `«Unit»`.
2. The `MERGEFIELD Reference`, `MERGEFIELD Description`, and `MERGEFIELD Currency` fields in neighboring material-row cells use the **complex** representation (`w:fldChar` begin/separate/end plus `w:instrText`). Their cached text is present as runs; Word split some visible labels across text runs and inserted `w:proofErr` spelling markers.
3. Without normalization, the PO observed missing labels/values in some of those complex-field cells as well as the simple `Unit` cell. With normalization, the same template and generated result rendered the table values.

**Important distinction:** The confirmed parser omission explains why the `Unit` simple field disappears, but it does not directly explain why adjacent complex fields are missing. A possible indirect interaction between an unsupported simple-field node and table/paragraph processing or rendering is **unproven**. The screenshots demonstrate the symptom; they do not isolate the mechanism.

A useful follow-up upstream regression experiment would compare the **same Word-resaved DOCX** (a) unchanged, (b) with only the `Unit` simple field unwrapped, and (c) with only that field removed, checking the rendered HTML/DOM for `Reference`, `Description`, `Currency`, and `Unit`. This would help distinguish a parser interaction from a layout/rendering effect. The Word-resaved comparison DOCX is not committed; recreate it using section 2.

## 5. Technical cause and selected mitigation

1. The Feature 004 Architect and independent Reviewer inspected installed `docx-preview` 0.4.1: its paragraph parsing path does not preserve paragraph-level `w:fldSimple` and their cached child runs. Consequently its HTML output silently drops the contained visible text. This cause is confirmed for the original programmatic sample.
2. `docxPreviewNormalization.ts` loads a **private ZIP copy** using pinned direct `jszip` 3.10.2. It processes supported Word story parts only: `word/document.xml`, numbered headers/footers, footnotes and endnotes. It recognizes both transitional and strict WordprocessingML namespaces.
3. For directly paragraph-contained simple fields, it replaces the wrapper with its existing children **in order**; it does not evaluate instructions, calculate field values, create fallback text, or rewrite unsupported nested shapes. Missing cached content stays empty.
4. It passes the normalized Blob solely to `docx-preview`. The selected original File remains the API upload, and the exact original API response Blob remains the Download source. No Core/API/CLI change is required.
5. The isolated workaround was checked by focused tests for field content, non-field preservation, namespace/story coverage, malformed input, byte integrity, and stale-render lifecycle; Feature 004 independent scoped implementation review reported **PASS**. The PO subsequently confirmed both Template and Result visually in a real browser.

## 6. Decision and limitations

1. **Keep browser-only normalization.** Simply resaving in Word was shown to be an incomplete workaround for these samples.
2. **No field-format rejection or special validation** in Core or UI: `w:fldSimple` is valid OOXML.
3. **Document the renderer limitation, not a document-invalidity claim.** The downloadable DOCX is authoritative; browser preview fidelity can differ from Microsoft Word for other constructs too.
4. Extra browser ZIP/XML work consumes CPU and memory; no server-side normalization, worker, size limit, or renderer fork was approved. Reevaluate if users encounter actual performance issues.
5. Report the underlying problem upstream in `docx-preview` separately. When a release fixes it, verify the repository sample, a Word-resaved comparison recreated using section 2, and their generated results **with normalization disabled** before removing the workaround and its direct JSZip dependency if otherwise unused.

## 7. Traceability

1. `.docs/features/004-monoreport-ui/implementation-plan.md` — Architect investigation (section 13), follow-up plan (section 14), independent review (section 15), scoped PO authorization (section 16).
2. `.docs/features/004-monoreport-ui/review.md` — scoped implementation evidence and independent PASS (sections 22–23).
3. `MonoReport.UI/src/services/docxPreviewNormalization.ts` — removable compatibility implementation; corresponding `.test.ts` contains focused fixtures.
4. Upstream issue: https://github.com/VolodymyrBaydalka/docxjs/issues/237 ; npm package: https://www.npmjs.com/package/docx-preview .

This note separates file-level inspection and observed browser behavior from inferences about library internals; do not use it as proof that every complex-field edge case is supported.
