# DOCX Template Merge Engine

## Feature 001 registration context

This document registers the original specification and the existing implementation at version `0.0.1` as Feature `001`.

Retrospective implementation review and verification are pending. No implementation plan is retroactively approved, and no Product Owner acceptance or feature completion is recorded by this registration. Existing staging tensions, including future capabilities listed in the test section, are preserved for later review rather than resolved through this migration.

## 1. Goal

Implement a free/open-source engine in C# that generates DOCX from a Word template and JSON data.

Input:

1. DOCX template;
2. JSON data.

Output:

3. DOCX with substituted values.

Core principles:

4. Use the standard Word `MERGEFIELD` as the basic templating mechanism;
5. Do not require installation of a Word Add-in;
6. Use the Open XML SDK;
7. Do not use commercial libraries;
8. Preserve template fields in the resulting DOCX.

A Word Add-in may be implemented in the future as a convenient UI for creating fields, but the engine must not depend on it.

---

# 2. Technologies

9. C# / .NET;
10. `DocumentFormat.OpenXml` / Open XML SDK;
11. No Aspose.Words, Syncfusion, or other commercial DOCX libraries.

The engine must work directly with `.docx` through OOXML.

---

# 3. Architecture

Separate the system into logical layers:

```text
DOCX
  │
  ▼
OOXML parser
  │
  ▼
Template model
  │
  ├── fields
  ├── regions
  └── document structure
  │
  ▼
Data resolver
  │
  ▼
Formatter
  │
  ▼
DOCX renderer
  │
  ▼
DOCX
```

Do not mix:

12. Working with OOXML;
13. Finding and parsing field codes;
14. Resolving JSON;
15. Formatting values;
16. Modifying DOCX.

The core engine must be a library. The HTTP API is a separate layer.

---

# 4. Basic MERGEFIELD

Support the standard Word field:

```text
{ MERGEFIELD CompanyName }
```

JSON:

```json
{
  "CompanyName": "Acme Ltd."
}
```

Result:

```text
Acme Ltd.
```

Also support:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

Important: after merging, `MERGEFIELD` **must not be deleted and converted into plain text**.

The field code must remain in the document, and the cached result must be updated.

---

# 5. OOXML fields

Account for Word representing a field with multiple XML elements.

For example:

```xml
<w:fldChar w:fldCharType="begin"/>
<w:instrText> MERGEFIELD CompanyName </w:instrText>
<w:fldChar w:fldCharType="separate"/>
...
<w:fldChar w:fldCharType="end"/>
```

Other valid OOXML representations are also possible.

Do not assume:

17. That the entire field code is in one XML node;
18. That the field code is in one `w:r`;
19. That the result is in one `w:r`.

A separate parser is needed to convert an OOXML field into a logical representation, for example:

```csharp
MergeField
{
    Name = "CompanyName",
    Switches = ...
}
```

---

# 6. JSON path

Support simple fields:

```text
CompanyName
```

And nested fields:

```text
Customer.Name
Customer.Address.City
Contract.Number
```

For example:

```json
{
  "Contract": {
    "Number": "C-0000279"
  },
  "Customer": {
    "Name": "Acme Ltd."
  }
}
```

The field:

```text
{ MERGEFIELD Customer.Name }
```

Must produce:

```text
Acme Ltd.
```

Dot notation is sufficient for the first stage.

---

# 7. Standard MERGEFIELD switches

Support the main standard Word switches.

## `\*`

Text/general formatting:

```text
{ MERGEFIELD Name \* Upper }
{ MERGEFIELD Name \* Lower }
{ MERGEFIELD Name \* Caps }
{ MERGEFIELD Name \* FirstCap }
{ MERGEFIELD Name \* MERGEFORMAT }
```

`MERGEFORMAT` is not a value transformation.

It concerns preserving/using the formatting of the field result.

It must not be confused with `Upper`, `Lower`, and similar switches.

---

## `\#`

Numeric formatting:

```text
{ MERGEFIELD Amount \# "#,##0.00" }
```

---

## `\@`

Date formatting:

```text
{ MERGEFIELD ContractDate \@ "dd.MM.yyyy" }
```

---

## Combining switches

Different standard switches may be used together, for example:

```text
{ MERGEFIELD Amount \# "#,##0.00" \* MERGEFORMAT }
```

Use an engine extension for spelling out monetary amounts:

```text
{ MERGEFIELD Totals.Amount \* SpellOut }
```

`SpellOut` is specified directly in the field code and handled by the MonoReport engine through a custom formatter. It is an engine extension, not built-in Word formatting. No content control tags or formatter parameters in the CLI are required. A field with the same path without this switch is rendered normally. The field code, including `SpellOut`, is preserved during merging.

---

# 8. Formatter architecture

Create an extensible formatter system.

Conceptually:

```csharp
formatter.Format(value, formatterName, options)
```

Standard Word switches must be handled separately from our custom formatters.

The architecture must allow the following to be added in the future:

```text
upper
lower
number
date
currency
spellOut
```

However, a large set of custom formatters should not be implemented in the first stage.

---

# 9. Monetary amounts in words

Provide for a custom formatter:

```text
spellOut
```

For example:

```json
{
  "Amount": 125430.50
}
```

May be converted to:

```text
Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек
```

Support:

20. RUB;
21. Russian ruble forms: рубли/рубля/рублей;
22. Russian kopeck forms: копейка/копейки/копеек;
23. Negative values.

Make the architecture extensible to other currencies.

The implementation of `spellOut` may be a separate stage.

---

# 10. Preserving the cached result

When processing:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

The engine must:

24. Find the field;
25. Preserve its field code;
26. Retrieve the value;
27. Apply formatting;
28. Replace the cached result;
29. Preserve the field in the DOCX.

For example:

```xml
<w:fldSimple w:instr=" MERGEFIELD CompanyName \* MERGEFORMAT ">
    <w:r>
        <w:rPr>
            <w:b/>
        </w:rPr>
        <w:t>Acme Ltd.</w:t>
    </w:r>
</w:fldSimple>
```

After the next merge:

```text
Northwind Inc.
```

Only the field result must change, not its field code.

---

# 11. Preserving formatting

When replacing the result, preserve `w:rPr`.

For example, if the field in the template is bold:

```xml
<w:rPr>
    <w:b/>
</w:rPr>
```

The new value must also be bold after merging.

Do not lose:

30. bold;
31. italic;
32. underline;
33. font;
34. font size;
35. color;
36. highlighting;
37. language;
38. Other `w:rPr` properties.

Covering this with tests is particularly important.

---

# 12. Re-merging

The following scenario must be supported:

```text
template
    ↓
merge(data1)
    ↓
document1
    ↓
merge(data2)
    ↓
document2
```

After the second merge:

39. The field code remains;
40. The value is updated;
41. Formatting is not lost;
42. The document remains a valid DOCX.

---

# 13. Collections / Mail Merge Regions

For collections, use semantics similar to Word Mail Merge Regions.

Region start:

```text
{ MERGEFIELD TableStart:Items }
```

Region end:

```text
{ MERGEFIELD TableEnd:Items }
```

These are special technical fields.

They are not ordinary data fields.

---

# 14. Table with a collection

JSON:

```json
{
  "Items": [
    {
      "Name": "Portland Cement",
      "Quantity": 2,
      "Price": 100
    },
    {
      "Name": "Concrete Blocks",
      "Quantity": 3,
      "Price": 200
    }
  ]
}
```

Conceptual template:

```text
┌─────────────────────────────────────┐
│ Product name │ Qty │ Price       │
├─────────────────────────────────────┤
│ TableStart:Items                    │
├─────────────────────────────────────┤
│ Name          │ Quantity │ Price    │
├─────────────────────────────────────┤
│ TableEnd:Items                      │
└─────────────────────────────────────┘
```

Inside a region, ordinary fields are resolved relative to the current item:

```text
{ MERGEFIELD Name }
{ MERGEFIELD Quantity }
{ MERGEFIELD Price }
```

Do NOT use:

```text
{ MERGEFIELD Items.Name }
{ MERGEFIELD Items.Quantity }
```

Inside the region.

---

# 15. Collection context semantics

For:

```text
TableStart:Items
```

A context is created:

```text
current = Items[i]
```

Therefore:

```text
{ MERGEFIELD Name }
```

Means:

```text
Items[i].Name
```

And:

```text
{ MERGEFIELD Quantity }
```

Means:

```text
Items[i].Quantity
```

After:

```text
TableEnd:Items
```

The current context returns to the parent context.

Use a context stack for the implementation.

---

# 16. Repeating table rows

Create a copy of the template region for each item in `Items`.

For example:

```text
TableStart:Items
    template row
TableEnd:Items
```

For two items, becomes:

```text
row for Items[0]
row for Items[1]
```

Technical `TableStart` / `TableEnd` fields do not appear in the result.

---

# 17. Empty collection

For:

```json
{
  "Items": []
}
```

No data rows must appear.

If the table header is outside the region:

```text
Header
TableStart:Items
Data row
TableEnd:Items
```

The header is preserved.

Technical rows containing only `TableStart` / `TableEnd` do not appear in the final document.

---

# 18. Nested collections

The architecture must allow:

```json
{
  "Orders": [
    {
      "Number": "A-001",
      "Items": [
        {
          "Name": "Portland Cement",
          "Quantity": 2
        },
        {
          "Name": "Concrete Blocks",
          "Quantity": 3
        }
      ]
    }
  ]
}
```

Template:

```text
TableStart:Orders

{ MERGEFIELD Number }

TableStart:Items
{ MERGEFIELD Name }
{ MERGEFIELD Quantity }
TableEnd:Items

TableEnd:Orders
```

Context:

```text
Orders[i]
    ↓
Items[j]
```

After `TableEnd:Items`:

```text
Orders[i]
```

After `TableEnd:Orders`:

```text
parent context
```

Full implementation of nested regions may follow the basic version, but the architecture must account for a context stack from the beginning.

---

# 19. Region parser

The parser must recognize `TableStart:Items` and `TableEnd:Items` as special elements:

```csharp
RegionStart
{
    Collection = "Items"
}
```

And:

```csharp
RegionEnd
{
    Collection = "Items"
}
```

The parser must validate correct nesting:

```text
TableStart:Items
TableEnd:Items
```

And detect errors:

```text
TableEnd:Items
```

Without a matching start.

Also detect mismatches:

```text
TableStart:Items
TableEnd:Orders
```

---

# 20. Conditions

Provide for conditions after basic collections.

Possible future semantics:

```text
IF
```

Or separate conditional regions.

Support in the future:

43. true/false;
44. Missing values;
45. Comparisons;
46. Conditional blocks.

Do not execute arbitrary code from a template.

Conditions must not become a business logic mechanism.

---

# 21. Business logic

JSON must be prepared before being passed to the engine.

For example, the calculation:

```text
VAT = Amount * 0.20
```

Must be performed by the application before calling the merge engine.

The merge engine handles:

```text
value
  ↓
formatting
  ↓
rendering
```

Rather than business calculations.

---

# 22. Errors

Provide diagnosable errors.

Unknown field:

```text
{ MERGEFIELD Customer.Unknown }
```

For example:

```text
Unknown template field: Customer.Unknown
```

Provide configurable behavior for a missing value:

47. empty string;
48. warning;
49. error.

The first version may use `empty string`.

An invalid formatter must report:

50. The field name;
51. formatter;
52. The original field code.

Region errors must report:

53. The collection name;
54. The error type;
55. The location in the document, if possible.

---

# 23. Core API

Implement a library first, for example:

```csharp
MergeResult Merge(
    Stream template,
    JsonDocument data);
```

Or an equivalent API.

Do not start the implementation with an HTTP microservice.

Build the HTTP API on top of the core library in a separate stage.

---

# 24. HTTP API — future stage

In the future:

```http
POST /api/templates/render
```

Accepts:

56. DOCX template;
57. JSON data.

Returns:

58. generated DOCX.

A specific transport/API contract is not currently part of the first stage.

---

# 25. Tests

Automated tests are mandatory.

Minimum set:

### Basic fields

59. Simple `MERGEFIELD`;
60. Nested JSON path;
61. Missing field.

### Formatting

62. `Upper`;
63. `Lower`;
64. `Caps`;
65. `FirstCap`;
66. `MERGEFORMAT`;
67. `\#`;
68. `\@`.

### OOXML

69. Field code in multiple XML nodes;
70. Field code in multiple runs;
71. Result in multiple runs;
72. Preserving bold;
73. Preserving italic;
74. Preserving font;
75. Preserving font size;
76. Preserving other `w:rPr` properties.

### Re-merge

77. merge(template, data1);
78. merge(result, data2).

The field must remain valid after the second merge.

### Collections

79. `TableStart:Items`;
80. `TableEnd:Items`;
81. Collection with 0 items;
82. Collection with 1 item;
83. Collection with N items;
84. Multiple fields inside a region;
85. Formatting inside a region;
86. Header outside the region is preserved;
87. Technical start/end fields do not appear in the result.

### Nested collections

88. Nested `Orders → Items`;
89. Correct restoration of the parent context.

---

# 26. Implementation stages

## Stage 1 — basic engine

Implement:

90. Open XML reading/writing;
91. field parser;
92. `MERGEFIELD`;
93. JSON path;
94. cached result;
95. Preserving field code;
96. Preserving formatting;
97. `Upper`;
98. `Lower`;
99. `Caps`;
100. `FirstCap`;
101. `MERGEFORMAT`;
102. `\#`;
103. `\@`;
104. automated tests.

Do not implement:

105. collections;
106. conditions;
107. spellOut;
108. HTTP API.

---

## Stage 2 — collections

Implement:

109. `TableStart:Items`;
110. `TableEnd:Items`;
111. collection context;
112. Repeating table rows;
113. Empty collections;
114. Multiple fields inside a region;
115. Tests.

---

## Stage 3 — nested collections

Implement:

116. context stack;
117. nested regions;
118. nested collections;
119. Corresponding tests.

---

## Stage 4 — advanced formatting

Implement:

120. formatter registry;
121. `spellOut`;
122. currency;
123. Additional formatters.

For monetary amounts in words, use the agreed `\* SpellOut` syntax handled by the MonoReport engine. Introduce other extensions only with explicit agreement.

---

## Stage 5 — conditions

Implement conditional regions/blocks.

---

## Stage 6 — HTTP service

An ASP.NET Core service on top of the library.

---

## Stage 7 — Word Add-in

A separate project.

The Add-in must allow users to:

124. Select an available field;
125. Insert a `MERGEFIELD`;
126. Select formatting;
127. Create `TableStart` / `TableEnd`;
128. Select a collection;
129. Avoid working with field codes manually.

The Add-in is not required for the engine to operate.

---

# 27. Important constraints

130. Do not use commercial DOCX libraries.
131. Use the Open XML SDK.
132. Do not delete `MERGEFIELD` after merging.
133. Do not convert fields into plain text.
134. Do not assume the field code is in one XML node.
135. Do not lose `w:rPr`.
136. Do not mix business logic and the template engine.
137. Do not require a Word Add-in.
138. Use `\* SpellOut` as the agreed engine extension; do not add other nonstandard switches without explicit agreement.
139. The core engine must be independent of the HTTP API.
140. The architecture must allow formatter extensions.
141. The architecture must allow nested collection regions.
142. Do not implement an HTML/PDF renderer immediately — it is a potential future extension, but is not part of the current task.

---

# 28. Definition of Done — first stage

The following scenario must work completely.

The user creates the following in Word:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

JSON:

```json
{
  "CompanyName": "Acme Ltd."
}
```

After merging, a valid DOCX is produced in which:

143. `Acme Ltd.` is displayed;
144. The original formatting is preserved;
145. `MERGEFIELD` still exists;
146. `MERGEFORMAT` remains in the field code;
147. The document opens in Word without corruption.

Re-merging:

```json
{
  "CompanyName": "Northwind Inc."
}
```

Must change the result to:

```text
Northwind Inc.
```

While:

148. The field code is preserved;
149. Formatting is preserved;
150. The DOCX remains valid.

After successfully completing the first stage, proceed to `TableStart:Items` / `TableEnd:Items`.

## 29. Approved retrospective corrections — F1–F3

1. Approval basis: the Product Owner's request, “Feature 001 — Plan Corrections for Review Findings F1–F3”, explicitly approves the correction behavior recorded in this section. It authorizes architectural assessment and correction planning only. It does not approve implementation, the historical specification as a whole, version-stage acceptance scope, or Feature 001 acceptance. The original specification above and the retrospective findings remain preserved.
2. Correction scope is limited to findings F1, F2, and F3 in `review.md`. Do not introduce new template syntax, support nested cached fields or conflicting independent regions, perform a general renderer refactor, or resolve unrelated stage and collection re-merge questions through these corrections. Feature 001 remains in phase `review`.

### 29.1 F1 — Unicode-safe cached-result replacement

1. Replacement must never divide a valid UTF-16 surrogate pair between XML text nodes. Preserve the complete replacement value, existing field instructions and markers, every existing run, and its complete run properties.
2. The focused architectural correction belongs in `CachedResultWriter`: retain the existing run-length-based distribution and adjust an interior boundary when it would divide a high/low surrogate pair. Advance that boundary by one UTF-16 code unit so the complete scalar stays in the earlier run; derive the next offset from the adjusted length. Empty later runs are permitted and their formatting must remain intact.
3. This policy preserves Unicode scalar integrity; it introduces no new grapheme-cluster formatting contract or invalid-Unicode input policy. Existing tabs, line breaks, XML escaping, whitespace, simple/complex fields, and BMP behavior must remain supported.
4. Acceptance requires regression coverage for non-BMP characters at multiple result-run boundaries, uneven and exhausted run lengths, mixed BMP/non-BMP values, and repeated scalar merges. Successful results must serialize, validate through Open XML validation, contain the exact expected text, preserve instructions and complete run properties, and remain valid after a second merge.

### 29.2 F2 — Reject nested fields in MERGEFIELD cached results

1. A MERGEFIELD cached result must contain no nested Word field, regardless of the nested field's type. This restriction applies to ordinary MERGEFIELDs and technical region-marker MERGEFIELDs, in both simple and complex representations. The engine must reject this unsupported structure before modifying the document.
2. For a simple outer field, the cached-result boundary is its descendant result content. For a complex outer field, it is the content after its own separator and before its matching end. A complex field without a separator has no cached-result interval. A nested simple field element or nested complex field beginning inside those boundaries triggers rejection; nesting depth and the presence or absence of a nested separator do not exempt it.
3. Validation must inspect all Word field types, including fields such as PAGE and IF that are ignored by merge-value parsing. The current `OoxmlFieldParser` recognizes complex structure but returns only MERGEFIELD references; testing only its returned MERGEFIELD list would miss the confirmed PAGE defect. Add focused structural validation within the existing OOXML parsing responsibility, retaining field-kind information long enough to diagnose the unsupported structure without introducing rendering for those kinds.
4. Nesting within an instruction rather than within a MERGEFIELD cached result is outside this rejection policy. Independent sibling fields and ordinary non-merge fields must retain their existing behavior. This correction does not promise new evaluation behavior for existing Word fields.
5. Throw `TemplateException` identifying the outer MERGEFIELD name, the nested field type and name when available, the original outer code through the existing exception contract, and the document location when determinable. Explain that nested Word fields inside MERGEFIELD results are unsupported and instruct the author to move the nested field outside the outer MERGEFIELD result or replace the outer result with plain cached text. Do not silently remove field codes or return a partial document.
6. Acceptance requires simple and complex outer/nested combinations, nested PAGE and MERGEFIELD reproductions, another non-merge field type, deeper nesting, and nested fields without a separator. Positive cases must demonstrate independent sibling fields and ordinary non-merge fields remain preserved. Rejection checks must assert actionable context and verify the caller's template content is unchanged and no result is returned.

### 29.3 F3 — Reject conflicting independent region block layouts

1. Region support remains based on the existing sibling paragraph/table-row blocks selected by `RegionParser.GetBlocks`, including its preference for complete common table rows. Inline start/end markers for one supported region remain valid; normal fields in paragraphs remain valid.
2. Determine logical region parentage from properly nested marker intervals, retaining matched regions at every nesting depth for validation. At each logical parent, compare independent sibling regions' physical block footprints before any mutation. Different collection names are not required for independence: sequential regions using the same collection must also be checked.
3. A footprint is the inclusive sibling-block range from its start block through its end block, together with the descendant content that copying/removing those blocks affects. Reject independent ranges when they share any block or when a block from one range contains a block from the other. This detects sequential regions sharing a paragraph or row, multi-block regions sharing a boundary block, and intersections across containing rows, nested tables, and paragraphs. Marker-order mismatches continue to use existing structural rejection.
4. Do not classify a properly nested parent/child region pair as independent merely because their footprints intersect; intersection is inherent to supported nested collections. Validate sibling conflicts inside nested regions as well as at the top level, even when the input collection is empty, missing, or null. Preserve supported nested layouts, inline markers, independent disjoint regions, and context restoration.
5. Throw `TemplateException` identifying both conflicting collection names, an unsupported shared/overlapping-block layout, and location when available. Suggest placing independent regions in separate supported paragraph or table-row ranges with no shared boundary block. Preserve the original code and contextual exception architecture; do not expose detached-node exceptions as the primary diagnostic or return an incomplete document.
6. Acceptance requires the confirmed shared-paragraph reproduction, shared-row markers in different cells, shared-boundary multi-block ranges, same-name independent regions, ancestor/descendant physical intersections, and nested sibling conflicts with empty as well as populated parent collections. Positive controls must preserve ordinary fields, a single inline region, existing nested collections, nonoverlapping regions, field codes, formatting, and valid DOCX output.

### 29.4 Validation timing, diagnostics, and architecture boundaries

1. Perform structural F2/F3 preflight across every story part currently processed by `MergeEngine`: main document, headers, footers, footnotes, and endnotes. Complete preflight on all those original XML trees before the first cached-result replacement, marker removal, block copy/removal, or story-part write. This implements the approved before-mutation requirement without adding processing for new document parts.
2. Keep package enumeration and orchestration in `MergeEngine`, OOXML field-structure validation in the existing field-parser responsibility, and region nesting/block-conflict validation in the existing region-parser responsibility. Expose focused validation through the existing renderer collaboration as needed. Loaded original XML trees and any validation metadata are per-operation data, never reusable service state. Preserve constructor injection and existing public merge-result/error behavior.
3. Preflight must inspect nested original region templates without relying on data-dependent rendering or removing markers to discover them. Recursive clone rendering may retain defensive validation, but it cannot replace original-template preflight: an invalid region must be rejected even when zero items prevent its rendering.
4. Report a document-part URI and a determinable paragraph/table-row location based on the original XML tree. Use stable one-based document-order indices or an unambiguous structural path within that part; identify table/row context where available. Do not claim a Word page number. Keep raw runtime exceptions or stack traces out of the primary author-facing message and use existing `TemplateException` contextual properties.
5. Preserve caller-owned template streams and never return `MergeResult` after F2/F3 rejection. Existing CLI exception handling must produce an actionable stderr message without DOCX bytes on stdout or replacement/truncation of an existing output file; verify this behavior without introducing unrelated CLI changes.
6. Architectural risks to address in the implementation plan are loss of non-MERGEFIELD type information, confusing genuine nested regions with independent physical conflicts, incomplete checks when only outer regions are returned, locations derived after mutation, and data-dependent omission of invalid nested templates. A package-wide preflight retains XML trees for the duration of a merge; this limited per-operation memory tradeoff avoids a general rendering-model refactor.
7. Implementation approval remains pending. The Developer must map these correction acceptance criteria to reproducing regression tests and successful controls, build/test/Open XML/CLI checks, and a separately recorded practical Microsoft Word opening check. Independent plan review precedes Product Owner plan approval; successful automated checks alone do not establish actual Word opening or Feature 001 acceptance.
