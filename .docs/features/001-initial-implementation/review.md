# Feature 001 retrospective review — existing implementation 0.0.1

## 1. Review basis and outcome

1. Audited repository revision: `d62fdc0f52d9e37f0c55a569e059621f0e798731`. The worktree was clean at audit start. Scope is the actual current implementation, including existing collections, nested collections, custom formatter support, CLI, and samples; the registered version is `0.0.1`.
2. The Orchestrator read `AGENTS.md`, all four Feature Workflow Skills, `feature-spec.md`, and `feature-status.json`. The phase was exactly `review`. The Architect independently assessed architecture and the Reviewer independently inspected source/tests and executed isolated probes. This review does not substitute Developer self-review for independent review.
3. Overall assessment: the documented ordinary-field, formatting, collection, and RUB SpellOut scenarios are substantially implemented, but conformance is incomplete. Independent probes reproduced two major defects in valid cached-field content and one minor region-diagnostic defect. No critical finding was established. Reviewer outcome: changes required; not PASS for feature acceptance.
4. No historical approved implementation plan is present or inferred. The registration does not establish prior specification approval, implementation sequencing, Product Owner acceptance, or completion. This audit was explicitly authorized; fixes were not.
5. Only this review document is created. Application code, tests, specification, and samples are unchanged. No implementation plan is created. State remains `review`; no acceptance, completion, push, merge, or publication occurs.

## 2. Executed verification

1. Environment: macOS arm64, .NET SDK `10.0.102`, .NET target `net10.0`, VSTest `18.0.1`.
2. Orchestrator ran `dotnet build MonoReport.slnx -c Release --no-restore`: exit 0; all three projects built; zero warnings and zero errors. Existing restored assets were used; a fresh dependency restore was not checked. Build ran outside the sandbox because earlier session evidence established MSBuild interprocess communication restrictions.
3. Orchestrator ran `dotnet test MonoReport.slnx -c Release --no-build --no-restore --logger 'trx;LogFileName=feature001-audit.trx' --results-directory /private/tmp/feature001-audit-results`: exit 0; 90 passed, zero failed, zero skipped. TRX evidence: `/private/tmp/feature001-audit-results/feature001-audit.trx`. Tests ran outside the sandbox because VSTest requires a local communication socket.
4. Orchestrator ran `dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --json-file Samples/sample.json --output /private/tmp/feature001-audit-sample.docx --culture en-GB`: exit 0. This is CLI execution evidence; the automated CLI tests additionally validate generated/committed samples, expected values, fields, table layout, and selected formatting.
5. Independent Reviewer initially ran `dotnet run --project /private/tmp/monoreport-reviewer-probes/probes.csproj --configuration Release` and then ran the expanded nine-case harness with `dotnet run --project /private/tmp/monoreport-reviewer-probes/probes.csproj --configuration Release --no-restore > /private/tmp/monoreport-reviewer-probes/results.txt`; both commands exited 0 against existing Release assemblies. The temporary harness does not modify repository tests or code. It checks ordinary Definition of Done, non-BMP text, nested cached fields, overlapping region blocks, same-name nested regions, and collection re-merging. Probe outcomes are described in findings and scope notes below. A harness that catches expected exceptions can exit 0 while revealing product defects; its exit code is not a product conformance PASS.
6. No Microsoft Word opening/visual inspection was performed. Open XML validation is valuable schema evidence, but does not independently prove requirement 147. No performance benchmark, concurrent registration stress test, fresh package restore, or historical TDD/approval verification was performed.
7. Temporary harness and TRX paths are local audit evidence, not new mandatory project artifacts. Findings describe the inputs and results so the report remains useful if temporary files are removed.

## 3. Findings

### F1 — Major: valid non-BMP scalar text fails when split across cached runs

1. Source: `MonoReport/Services/CachedResultWriter.cs:62–66` distributes replacement text using UTF-16 lengths and `Substring`; serialization occurs in `MonoReport/Services/MergeEngine.cs:46`.
2. Reproducer: a valid MERGEFIELD cached result contains two text runs, each initially one character long. Resolve its Name value to the single emoji U+1F600. The first run receives the high surrogate and the next the low surrogate. Serializing separate text elements raises `ArgumentException` because a high surrogate lacks its following low surrogate within that element. Merge does not return a generated document.
3. Affected obligations: requirements 19, 28, 39–42, 92, and the general DOCX generation/validity and multi-run result obligations in specification sections 1, 5, 10, and 12. Ordinary ASCII/BMP and tested repeated merges work; the universal scalar/multi-run contract does not.
4. Classification: genuine basic-engine defect, not deferred functionality. Existing tests cover split runs and XML characters but omit surrogate boundaries. No failing regression test is added during this review.
5. Recommended correction, subject to Product Owner authorization: preserve Unicode scalar boundaries when distributing text while retaining run properties; first add regression coverage for non-BMP values at run boundaries, unequal cached-run lengths, and repeated merges. Define any stronger grapheme-cluster formatting policy separately rather than silently imposing it.

### F2 — Major: replacing an outer cached result deletes nested field codes

1. Source: `MonoReport/Services/CachedResultWriter.cs:23–29` removes every cached-result run child except text and run properties. `MonoReport/Models/FieldReference.cs:10–19` includes descendant nodes in result ranges; `MonoReport/Services/DocumentRenderer.cs:18–29` renders the parsed outer field before inner cached fields.
2. Reproducer A: outer MERGEFIELD Name has a valid nested complex PAGE field in its cached result. Merge succeeds and Open XML validation reports no errors, but the nested PAGE instruction/markers disappear. Schema validity alone therefore misses field loss.
3. Reproducer B: outer MERGEFIELD Name has nested complex MERGEFIELD Inner in its cached result. Rendering the outer field removes the inner instruction/markers. The subsequent inner write operates on detached nodes and raises `InvalidOperationException` with message “The parent is missing.”
4. Affected requirements: 8, 25, 29, 39–40, 92, 95, 132–133, and specification sections 4–5 and 10–12. Fragmented instructions, ordinary complex fields, and sibling non-merge fields are verified, but that does not cover nested result content.
5. Classification: genuine field/code preservation defect, not a future formatter or collection feature. The parser recognizes nested complex fields, while result mutation has no ownership boundary. Existing tests do not cover nested fields inside cached results.
6. Recommended correction, subject to authorization: define owned cached-result nodes and nested-field handling before mutation, preserve inner codes or explicitly diagnose unsupported nesting without silently deleting them, and add regression tests for nested PAGE and MERGEFIELD content and repeated merges. Any policy that narrows the preservation contract requires Product Owner approval.

### F3 — Minor: sibling regions sharing a paragraph cause an uncontextualized exception

1. Source: `MonoReport/Services/DocumentRenderer.cs:32–35,79–81` processes each parsed region using mutable whole-block anchors; `MonoReport/Services/RegionParser.cs:48–68` maps markers to sibling rows/paragraphs.
2. Reproducer: one paragraph contains a complete A region followed by a complete B region, each with an item Name field. Both collections contain one item. Processing removes their shared paragraph during the first region; the second accesses a detached block and raises `NullReferenceException` instead of a contextual `TemplateException`.
3. Affected diagnostic requirements: 53–55 and specification section 22. Existing unmatched-marker, mismatch, and non-array errors are contextualized and tested. This accepted parser layout is not validated before mutation.
4. Classification: confirmed diagnostic/validation defect plus an unresolved layout boundary. The specification does not clearly require partial-paragraph region expansion; this review does not add that requirement or demand a new rendering mode.
5. Recommended correction, subject to authorization: establish the supported layout boundary with the Product Owner, validate overlapping/shared block ranges before mutation, and report collection, error type, and available document location for rejected layouts.

## 4. Architecture assessment

1. Logical services exist for package IO (`MergeEngine`), field/region parsing, JSON resolution, standard/custom formatting, rendering, and cached-result mutation. The core is an independent library with Open XML SDK as its DOCX dependency; there is no commercial DOCX library, Word/Add-in dependency, HTTP coupling, arbitrary template-code evaluator, or business-calculation subsystem.
2. Inspected production services follow project Coding Guidelines: class instances, external constructor dependency injection, null validation and primary constructors where appropriate. The consuming CLI composes the dependency graph at `MonoReport.Cli/Program.cs:10–17`; tests compose their own graph. No public/internal static service logic was found. Private static helpers and the CLI entry point are not violations.
3. `MergeContext` stores per-operation values/options/warnings. `DocumentRenderer.cs:64–72` restores pushed contexts in `finally`. The reuse test verifies that missing-value options and warnings do not leak across sequential operations.
4. Minor advisory boundary concern: `FieldReference` exposes XML nodes, and `DocumentRenderer` combines orchestration with block cloning/insertion/removal. Logical boundaries satisfy the current service split, but independent template models/renderers would require more separation. F2 demonstrates a concrete ownership problem; a general refactor is not automatically required by this review.
5. Advisory scalability concern: `FieldReference.Range` rebuilds root descendant lists per complex field, and rendering repeatedly scans region membership. No performance acceptance criterion exists, so this is not a failed requirement or benchmark claim.
6. Formatter interfaces, registry injection/registration, and options demonstrate extensibility. SpellOut dispatch references the RUB formatter-name constant, but replacing that registry entry remains possible. Concurrent mutation of the registry is unsynchronized; concurrent registration is not promised by the specification and was not tested.

## 5. Requirements coverage

1. The matrix below covers every numbered requirement 1–150 exactly once. “Implemented and verified” means supported by executed tests/probes or direct source/project inspection for structural/prohibition requirements; it is not a guarantee over all possible Word documents. A partial capability does not make its existing ordinary-case test absent.
2. Broad preservation/re-merge obligations are partial where F1/F2 apply. Requirements 143–146 and 148–150 refer to the specific ordinary scalar Definition of Done and are verified separately. Requirement 147 retains its manual Word verification gap.
3. Future-stage items are marked not applicable for this retrospective baseline because their specifications explicitly defer them; that classification does not establish an approved version-stage boundary. Stage 1 exclusions 105–108 are sequencing instructions, not a demand to remove currently shipped collections or SpellOut. The Product Owner must resolve the historical scope ambiguity.
4. Requirements 122–123 do not name a required non-RUB currency, currency display contract, or additional production formatter catalog. Existing RUB and custom registration are verified; no broader catalog is silently required or declared implemented.
5. Totals: 114 implemented and verified; 1 implemented but not verified; 16 partially implemented; 0 not implemented within the applicable baseline; 19 not applicable to version 0.0.1. Deferred functionality remains absent, as detailed below.


| Requirement | Original obligation | Classification | Evidence / limit |
| --- | --- | --- | --- |
| 1 | DOCX template | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 2 | JSON data | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 3 | DOCX with substituted values | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 4 | Use the standard Word `MERGEFIELD` as the basic templating mechanism | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 5 | Do not require installation of a Word Add-in | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 6 | Use the Open XML SDK | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 7 | Do not use commercial libraries | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 8 | Preserve template fields in the resulting DOCX | Partially implemented | F2: nested cached-field loss/crash |
| 9 | C# / .NET | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 10 | `DocumentFormat.OpenXml` / Open XML SDK | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 11 | No Aspose.Words, Syncfusion, or other commercial DOCX libraries | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 12 | Working with OOXML | Implemented and verified | Independent architecture inspection; advisory DOM coupling |
| 13 | Finding and parsing field codes | Implemented and verified | Independent architecture inspection; advisory DOM coupling |
| 14 | Resolving JSON | Implemented and verified | Independent architecture inspection; advisory DOM coupling |
| 15 | Formatting values | Implemented and verified | Independent architecture inspection; advisory DOM coupling |
| 16 | Modifying DOCX | Implemented and verified | Independent architecture inspection; advisory DOM coupling |
| 17 | That the entire field code is in one XML node | Implemented and verified | FieldTests complex fragmented-code scenarios |
| 18 | That the field code is in one `w:r` | Implemented and verified | FieldTests complex fragmented-code scenarios |
| 19 | That the result is in one `w:r` | Partially implemented | F1: non-BMP split-run failure |
| 20 | RUB | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 21 | Russian ruble grammatical forms | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 22 | Russian kopeck grammatical forms | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 23 | Negative values | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 24 | Find the field | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 25 | Preserve its field code | Partially implemented | F2: nested cached-field loss/crash |
| 26 | Retrieve the value | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 27 | Apply formatting | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 28 | Replace the cached result | Partially implemented | F1: non-BMP split-run failure |
| 29 | Preserve the field in the DOCX | Partially implemented | F2: nested cached-field loss/crash |
| 30 | bold | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 31 | italic | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 32 | underline | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 33 | font | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 34 | font size | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 35 | color | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 36 | highlighting | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 37 | language | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 38 | Other `w:rPr` properties | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 39 | The field code remains | Partially implemented | F1/F2; ordinary scenarios pass |
| 40 | The value is updated | Partially implemented | F1/F2; ordinary scenarios pass |
| 41 | Formatting is not lost | Implemented and verified | FieldTests; complete rPr comparisons on repeated merges |
| 42 | The document remains a valid DOCX | Partially implemented | F1: non-BMP split-run failure |
| 43 | true/false | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 44 | Missing values | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 45 | Comparisons | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 46 | Conditional blocks | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 47 | empty string | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 48 | warning | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 49 | error | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 50 | The field name | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 51 | formatter | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 52 | The original field code | Implemented and verified | FieldTests missing-value/format errors; FormatterTests context |
| 53 | The collection name | Partially implemented | F3; normal structure-error tests pass |
| 54 | The error type | Partially implemented | F3; normal structure-error tests pass |
| 55 | The location in the document, if possible | Partially implemented | F3; normal structure-error tests pass |
| 56 | DOCX template | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 57 | JSON data | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 58 | generated DOCX | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 59 | Simple `MERGEFIELD` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 60 | Nested JSON path | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 61 | Missing field | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 62 | `Upper` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 63 | `Lower` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 64 | `Caps` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 65 | `FirstCap` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 66 | `MERGEFORMAT` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 67 | `\#` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 68 | `\@` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 69 | Field code in multiple XML nodes | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 70 | Field code in multiple runs | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 71 | Result in multiple runs | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 72 | Preserving bold | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 73 | Preserving italic | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 74 | Preserving font | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 75 | Preserving font size | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 76 | Preserving other `w:rPr` properties | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 77 | merge(template, data1) | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 78 | merge(result, data2) | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 79 | `TableStart:Items` | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 80 | `TableEnd:Items` | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 81 | Collection with 0 items | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 82 | Collection with 1 item | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 83 | Collection with N items | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 84 | Multiple fields inside a region | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 85 | Formatting inside a region | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 86 | Header outside the region is preserved | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 87 | Technical start/end fields do not appear in the result | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 88 | Nested `Orders → Items` | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 89 | Correct restoration of the parent context | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 90 | Open XML reading/writing | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 91 | field parser | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 92 | `MERGEFIELD` | Partially implemented | F1/F2; ordinary scenarios pass |
| 93 | JSON path | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 94 | cached result | Partially implemented | F1: non-BMP split-run failure |
| 95 | Preserving field code | Partially implemented | F2: nested cached-field loss/crash |
| 96 | Preserving formatting | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 97 | `Upper` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 98 | `Lower` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 99 | `Caps` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 100 | `FirstCap` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 101 | `MERGEFORMAT` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 102 | `\#` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 103 | `\@` | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 104 | automated tests | Implemented and verified | FieldTests/OoxmlEdgeTests; Docx.AssertValid; full suite |
| 105 | collections | Not applicable to version 0.0.1 | Stage 1 sequencing; later behavior exists; approval scope unresolved |
| 106 | conditions | Not applicable to version 0.0.1 | Stage 1 exclusion; future behavior absent |
| 107 | spellOut | Not applicable to version 0.0.1 | Stage 1 sequencing; later behavior exists; approval scope unresolved |
| 108 | HTTP API | Not applicable to version 0.0.1 | Stage 1 exclusion; future behavior absent |
| 109 | `TableStart:Items` | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 110 | `TableEnd:Items` | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 111 | collection context | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 112 | Repeating table rows | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 113 | Empty collections | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 114 | Multiple fields inside a region | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 115 | Tests | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 116 | context stack | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 117 | nested regions | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 118 | nested collections | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 119 | Corresponding tests | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 120 | formatter registry | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 121 | `spellOut` | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 122 | currency | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 123 | Additional formatters | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 124 | Select an available field | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 125 | Insert a `MERGEFIELD` | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 126 | Select formatting | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 127 | Create `TableStart` / `TableEnd` | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 128 | Select a collection | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 129 | Avoid working with field codes manually | Not applicable to version 0.0.1 | Explicit future-stage functionality; not a baseline defect |
| 130 | Do not use commercial DOCX libraries | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 131 | Use the Open XML SDK | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 132 | Do not delete `MERGEFIELD` after merging | Partially implemented | F2: nested cached-field loss/crash |
| 133 | Do not convert fields into plain text | Partially implemented | F2: nested cached-field loss/crash |
| 134 | Do not assume the field code is in one XML node | Implemented and verified | FieldTests complex fragmented-code scenarios |
| 135 | Do not lose `w:rPr` | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 136 | Do not mix business logic and the template engine | Implemented and verified | Resolver/formatter/renderer source; no business evaluation |
| 137 | Do not require a Word Add-in | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 138 | Use `\* SpellOut` as the agreed engine extension; do not add other nonstandard switches without explicit agreement | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 139 | The core engine must be independent of the HTTP API | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 140 | The architecture must allow formatter extensions | Implemented and verified | FormatterTests and TemplateFormatterTests; registry inspection |
| 141 | The architecture must allow nested collection regions | Implemented and verified | RegionTests/OoxmlEdgeTests; context stack; validator |
| 142 | Do not implement an HTML/PDF renderer immediately — it is a potential future extension, but is not part of the current task | Implemented and verified | Core/project inspection; build; CLI and FieldTests |
| 143 | `Acme Ltd.` is displayed | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 144 | The original formatting is preserved | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 145 | `MERGEFIELD` still exists | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 146 | `MERGEFORMAT` remains in the field code | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 147 | The document opens in Word without corruption | Implemented but not verified | OpenXmlValidator passes; Microsoft Word not opened |
| 148 | The field code is preserved | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 149 | Formatting is preserved | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |
| 150 | The DOCX remains valid | Implemented and verified | Ordinary DoD probe; FieldTests remerge/rPr/code; validator |

## 6. Unnumbered requirements and acceptance obligations

1. Section 1: library generation from DOCX plus JSON, direct Word MERGEFIELD templating, and no Add-in dependency are implemented and verified for ordinary inputs. General output/preservation guarantees are partially implemented because of F1/F2.
2. Section 2: direct OOXML processing through Open XML SDK is implemented and verified by project/source inspection and executed builds/tests.
3. Section 3: distinct logical parser/model/resolver/formatter/renderer responsibilities and independent core library are implemented and verified structurally. DOM ownership and region mutation boundaries remain architectural advisories; the specification does not mandate a detached intermediate representation.
4. Sections 4–5: simple and complex fields, fragmented instructions, multi-run results, logical parser representations, and cached updates are implemented and verified in ordinary cases. Broad valid-field and preservation obligations are partially implemented because of F1/F2. Nested cached fields are not newly added requirements: they expose the existing broad field-preservation promise.
5. Section 6: simple and dotted object paths are implemented and verified by scalar/path tests; exact property lookup is used. Array indexing and parent lookup syntax are not specified or required.
6. Section 7: the named general switches, numeric/date examples, combinations, field-code-preserving SpellOut dispatch, and normal rendering of an identical path without SpellOut are implemented and verified by field/formatter tests. A separate probe also verified a negative number with a currency-like numeric picture. This is not a claim of compatibility with every historical Word picture-format grammar.
7. Sections 8–9: standard-switch handling is separate from custom registry dispatch; formatter options, custom registration, RUB declensions, negatives, and representative rounding/scales are implemented and verified. Non-RUB extensibility exists. A broader built-in formatter catalog is future-stage functionality with unspecified semantics, not an established defect.
8. Sections 10–12: ordinary cached-result replacement, complete run properties, repeated scalar merges, unchanged codes, and schema-valid outputs are implemented and verified. Universal preserved nested codes and multi-run scalar values are partially implemented because of F1/F2; collection re-merging is unresolved separately below.
9. Sections 13–17: technical region markers, item-relative fields, stack restoration, row copies, zero-item behavior, preserved external headers, and removal of purely technical blocks are implemented and verified for supported sibling-row/paragraph layouts. No arbitrary cross-container or partial-block region semantics are inferred. F3 identifies missing rejection diagnostics rather than a new required layout.
10. Section 18: nested collections and parent/root context restoration are implemented and verified. The additional isolated same-name nested-region case succeeds and produces both expected item values with retained fields.
11. Section 19: stack parsing and unmatched-start/end/mismatched-name detection are implemented and verified. Overlapping physical block ranges are a separate unvalidated layout case in F3.
12. Section 20: conditional rendering is not applicable to the baseline because it is scheduled later. Absence of arbitrary code evaluation and business-expression execution is implemented and verified by source inspection. Existing Word IF preservation is not reported as conditional support.
13. Section 21: precomputed-data input and absence of business calculation are implemented and verified structurally and through sample arithmetic inspection. Formatting/spelling amounts is a display operation, not a business calculation.
14. Section 22: missing-value empty/warning/error choices and invalid-formatter context are implemented and verified; region error context is partially implemented because of F3. Existing part URI/context paths satisfy location information where available; the specification does not require page numbers or paragraph indices.
15. Section 23: Stream/JsonDocument library API, returned DOCX bytes/warnings, and an independent core are implemented and verified. Current source structure does not establish whether historical development followed the intended chronology; no approved plan or chronological evidence is fabricated.
16. Section 24: HTTP transport contract is intentionally future work and not applicable to the baseline.
17. Section 25: every explicitly listed ordinary-case automated test category is present and passed. This does not remove the edge-case gaps described in section 7 of this review.
18. Section 26: actual capabilities include Stages 1–3 and selected Stage 4 behavior. Completion of those stages and their historical approval gates is not inferred. Conditions, HTTP, and Add-in behavior remain future work; a version-stage decision requires Product Owner approval.
19. Section 27: noncommercial SDK use, library/HTTP separation, no Add-in dependence, no template business execution, specified SpellOut-only template extension, formatter/nested-region extensibility, and no immediate HTML/PDF renderer are implemented and verified structurally. Universal field preservation remains partial because of F2.
20. Section 28: the independent literal Acme Ltd. → Northwind Inc. scalar probe succeeds on both merges, validates both outputs, retains the exact field code including MERGEFORMAT, and preserves complete run properties. Microsoft Word opening remains implemented but not verified. Automated success does not constitute Product Owner acceptance.

## 7. Test coverage and quality

1. Strong evidence: `MonoReport.Tests/Services/Docx.cs:65,91` invokes `OpenXmlValidator` for helper-mediated successful merges. Other sample/CLI assertions also validate DOCX bytes. Results are checked for values, field counts/codes, complete run-property equivalence, and container preservation rather than merely testing for no exception.
2. `FieldTests.Remerge_preserves_code_and_all_run_properties` and `Complex_field_handles_fragmented_code_and_multiple_formatted_result_runs` compare preserved instructions and complete properties across repeated merges. The tests cover simple/complex representations, missing cached content, multiple runs, whitespace, tabs/breaks, XML characters, missing values, and invalid-format context.
3. `RegionTests` covers zero/one/many items, multiple fields, row formatting, external headers, technical-marker removal, inline markers in a data row, nested paragraph/table regions, independent nonoverlapping regions, parent/root restoration, malformed nesting, and non-array rejection.
4. `FormatterTests` and `TemplateFormatterTests` cover RUB grammatical forms, representative scales/negatives/rounding, custom registry options, missing formatters, explicit culture, SpellOut-only versus normal numeric fields, complex/simple field preservation, combinations, repeated merges, and collection spelling.
5. `CliTests` verifies four JSON input modes, output files versus binary stdout, stderr failures, preservation of existing output on invalid JSON, options/help, sample validity, expected material rows, headings, shading, fields, and other selected formatting. These tests use an actual committed user sample; that fixture coupling is a maintenance tradeoff, not a specification violation.
6. Coverage gaps: non-BMP run boundaries, nested fields inside cached results, overlapping block diagnostics, collection re-merge expectations, comments and successful footnote/endnote rendering, missing/null/primitive collection behavior, complex cloned content/IDs, formatter limit/unsupported-currency/failure behavior, and actual Microsoft Word opening. Source supports footnotes/endnotes, but no dedicated successful story-part test was found. Comments processing is not expressly required by the current specification, so absence of a comments test is not independently classified as a defect.
7. No line/branch coverage measurement was run. Test quantity is not a coverage percentage. No repository regression tests were added because this is a review-only task; approved correction work should first turn the confirmed probes into regression tests.

## 8. Stage and requirement ambiguities

1. Version 0.0.1 has no recorded approved stage boundary. Collections, nested collections, registry, and SpellOut are present despite Stage 1 sequencing exclusions. This audit reviews those existing capabilities but does not retrospectively approve their scope or conclude they should be removed.
2. Conditions, an HTTP service, a Word Add-in, and a broader non-RUB/additional formatter catalog are absent and explicitly staged for later. Their absence is deferred functionality, not a confirmed missing applicable baseline requirement. Product Owner approval is required if any should become part of this version's acceptance scope.
3. Repeated merge is mandatory for ordinary scalar fields, while collection technical markers must be removed. An isolated probe renders a one-item collection, then merges its output with a changed collection. Output remains schema-valid with its retained Name field, but that field becomes empty because the root has no Name and region/item bindings are gone. The current implementation cannot regenerate collection structure from rendered output. Record this as a specification/acceptance boundary requiring clarification, not a silently imposed persistent-template requirement.
4. Supported collection block boundaries are sibling rows/paragraphs. Whether multiple independent regions may share a physical paragraph or row requires an explicit policy. F3 remains a contextual error-reporting problem for the current parser/rendering combination even if that layout is rejected.
5. Broad field preservation creates the F2 blocker for nested cached content. A decision to reject that content may be preferable to ambiguous mutation, but narrowing a valid-input/preservation promise cannot be silently treated as already approved.

## 9. Required decisions and next action

1. Product Owner should approve correction scope for F1 and F2 before implementation. The review recommends Unicode-safe cached-result distribution and explicit ownership/preservation of nested fields, each preceded by reproducing regression tests. This is a recommendation, not an implementation plan.
2. Product Owner should decide the supported overlapping-region layout boundary and whether F3 should be corrected by contextual rejection or additional rendering support.
3. Product Owner should clarify the version-stage acceptance scope and the collection re-merge contract. Preserve future-stage deferral unless the Product Owner explicitly changes it.
4. Arrange actual Word opening/visual verification for requirement 147 before claiming that criterion verified. Additional targeted story-part/formatter edge tests can be included in an approved verification scope.
5. After approved corrections and executed checks, require independent re-review. Feature 001 remains in phase `review`; acceptance/completion requires a separate explicit Product Owner decision. Stop after this audit and await further instructions.

## 10. Product Owner decisions for correction planning

1. On 2026-10-11 the Product Owner explicitly approved planning corrections for F1–F3: Unicode surrogate-pair integrity; rejection of nested Word fields inside MERGEFIELD cached results; and rejection of conflicting independent regions sharing or overlapping document blocks. Errors must identify affected names and determinable document context, suggest specific template corrections, and fail without corrupted output.
2. These decisions authorize architectural assessment, a prospective correction implementation plan, and independent plan review only. Implementation and test changes require separate explicit Product Owner plan approval. They do not retroactively approve a historical implementation plan or close any audit finding.
3. Approved behavior and the Architect's focused validation boundaries are recorded in feature-spec.md section 29. The prospective implementation-plan.md records the steps, acceptance mapping, and independent plan review. Original audit sections 1–9 and their evidence/classifications remain historical review results, not reclassified as fixed.
4. Version-stage scope, collection re-merging, and actual Microsoft Word opening remain separate concerns. No new product requirement or general renderer refactor is approved through this planning task. Feature status remains exactly review.

## 11. Correction-plan review outcome

1. The Developer created the prospective implementation-plan.md after the Architect's assessment. The independent Reviewer inspected the actual plan, approved correction decisions, and current source/tests and found no blocking plan findings. The plan-review criteria and outcome are recorded separately in implementation-plan.md.
2. PLAN PASS means the proposed correction work is ready for Product Owner consideration. It does not mean F1–F3 have been implemented or verified, and it does not supersede the retrospective implementation outcome of changes required. No application code or tests were modified or executed for correction planning.
3. Explicit Product Owner approval of the concrete implementation plan remains pending. The next action is to obtain that approval before correction implementation. Feature 001 remains in phase review; neither implementation success nor acceptance/completion is recorded.

## 12. Approved correction implementation — 2026-10-11

1. The Product Owner explicitly approved the existing PLAN PASS implementation plan in the request “Feature 001 — Implement Approved F1–F3 Corrections”. The approval authorizes ordered F1, F2, F3 implementation and its verification; it supersedes the pending correction-implementation gate recorded above without altering the historical audit. The approval is also recorded in implementation-plan.md section 12.
2. Feature 001 remains in review through correction implementation and independent review. Only after independent implementation review passes does the authorized transition to acceptance occur. Completion still requires explicit Product Owner acceptance. Manual desktop Microsoft Word verification remains unperformed unless a human supplies evidence.

## 13. Independent F1–F3 implementation review — 2026-10-11

1. Verdict: **PASS for the approved F1–F3 correction scope**. The independent Reviewer inspected the actual production and test diff, AGENTS.md, Reviewer Skill, original specification and approved section 29, prospective plan and explicit approval, historical findings, and executed verification evidence. No critical, major, or other blocking correction finding remains. This verdict does not accept the feature or reclassify unrelated historical requirements.
2. F1 is resolved: `MonoReport/Services/CachedResultWriter.cs:66` advances an interior split-surrogate boundary exactly one UTF-16 code unit and advances the next offset from the adjusted length. Existing run containers, complete properties, instructions, markers, control-character conversion, and final remainder behavior remain intact. Four simple/complex regression cases exercise unequal and empty runs, successive non-BMP boundaries, exhausted runs, longer replacements, and repeated Unicode/empty/BMP merges. Serialization and whole-package validation accompany exact text, code, marker, run-count, and complete-property comparisons. The actual CLI Unicode fixture additionally has distinct bold, italic, and underline runs, preserved in generated output.
3. F2 is resolved by the approved rejection policy: `MonoReport/Services/OoxmlFieldParser.cs:10` gathers all simple and complex field structures, checks their beginnings against the exact outer cached-result range, and preserves instruction nesting outside that range. The 72-case negative matrix covers all outer/nested representations, PAGE/MERGEFIELD/IF, absent cached content/separator, and ordinary/start/end outer MERGEFIELDs. Deeper nesting and sibling/instruction controls are covered separately. Diagnostics retain outer name/code, nested kind/name when available, original part and paragraph context, and concrete correction advice. Rejection precedes rendering and preserves caller bytes and service reuse.
4. F3 is resolved by the approved rejection policy: `MonoReport/Services/RegionParser.cs:8` retains all matched intervals and logical parents, calculates inclusive physical footprints through the existing block selector, and compares independent siblings for shared or ancestor-contained blocks. Genuine logical parent/child intersections are excluded from conflict comparisons. Twelve footprint cases cover different and identical names across paragraphs, rows, shared boundaries, and containing rows/nested tables. Eight nested-sibling cases cover two depths and missing/null/empty/populated parent data. Positive zero/one/many, disjoint, same-name nesting, existing row and nested-collection controls retain fields, formatting, context restoration, and valid output.
5. Section 29.4 and plan timing checks are verified: `MonoReport/Services/MergeEngine.cs:32` loads all five supported story kinds into local operation data, completes every preflight before entering the rendering/writing loop, and retains existing externally injected dependencies. Ten invalid-story cases assert zero formatter calls, and direct preflight checks compare whole XML snapshots after rejection. Successful five-story tests verify values, sibling PAGE preservation, technical-marker removal, formatting, valid package relationships, and engine reuse. No new service state, default dependency construction, new processing scope, or unrelated renderer redesign was introduced.
6. Test evidence was inspected independently: F1 Red recorded four surrogate serialization failures with 26 passes, then Green 30/30; F2 Red recorded PAGE silently succeeding and nested MERGEFIELD throwing InvalidOperationException, then Green 32/32; F3 Red recorded NullReferenceException instead of TemplateException with ten passes, then Green 11/11. TRX files are in `/private/tmp/feature001-corrections-evidence`. Environmental socket failures are not Red evidence. The additional compatibility and integration matrices ran after those reproductions and are not presented as separate historical Red runs.
7. Independently executed `dotnet build MonoReport.slnx -c Release --no-restore`: exit 0, zero warnings, zero errors. Independently executed `dotnet test MonoReport.slnx -c Release --no-build --no-restore --logger 'trx;LogFileName=independent-review.trx' --results-directory /private/tmp/feature001-independent-review`: exit 0, 215 passed, zero failed, zero skipped. These checks ran with approved execution outside the sandbox. The independent TRX is `/private/tmp/feature001-independent-review/independent-review.trx`.
8. Independently executed `dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --json-file Samples/sample.json --output /private/tmp/feature001-independent-review/sample.docx --culture en-GB`: exit 0 and empty output/error streams. Independently executed `dotnet /private/tmp/feature001-cli-verifier/bin/Release/net10.0/verifier.dll /private/tmp/feature001-independent-review/sample.docx`: exit 0, zero Open XML errors, expected construction values, retained fields, and four table rows including the header. The previously approved Russian RUB spell-out exception remains intact.
9. Independently executed the same built CLI with `--template /private/tmp/feature001-cli-fixtures/nested.docx --json-file /private/tmp/feature001-cli-fixtures/data.json --output /private/tmp/feature001-independent-review/invalid-nested.docx --culture en-GB` and with `--template /private/tmp/feature001-cli-fixtures/regions.docx --json-file /private/tmp/feature001-cli-fixtures/data.json --output /private/tmp/feature001-independent-review/invalid-regions.docx --culture en-GB`. Each returned exit 1 with actionable original-location diagnostics; neither output file exists. Orchestrator process evidence additionally verifies zero stdout bytes, new-file absence, existing-file sentinel preservation, input-template immutability, successful Unicode generation/re-merging, and independent structural/formatting comparisons in `/private/tmp/feature001-cli-smokes-76fmo8ex`.
10. Architecture and scope conform to the approved focused decisions. Per-operation retention of story XML is the explicitly approved memory tradeoff. The unrelated backlog edit is excluded from this correction verdict and preserved. Original retrospective sections 1–9 remain unchanged; F1–F3 are superseded only by this recorded disposition, while version-stage and collection re-merge decisions remain outside scope.
11. Manual desktop Microsoft Word opening and visual verification were not performed. Original requirement 147 remains pending human evidence; Open XML validation does not establish it. Feature completion and Product Owner acceptance remain pending. The Orchestrator owns the authorized transition from review to acceptance following this scoped PASS.

## 14. Executed verification evidence — 2026-10-11

1. Baseline Release build: `dotnet build MonoReport.slnx -c Release --no-restore`, exit 0, zero warnings/errors. Original-suite baseline: `dotnet test MonoReport.slnx -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Unicode_scalar_boundaries' --logger 'trx;LogFileName=feature001-corrections-baseline.trx' --results-directory /private/tmp/feature001-corrections-baseline`, exit 0, 90 passed, zero failed/skipped. The filter excludes the four newly added F1 regressions before production correction; it does not exclude an original test.
2. F1 Red: `dotnet test MonoReport.Tests/MonoReport.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FieldTests --logger 'trx;LogFileName=f1-red.trx' --results-directory /private/tmp/feature001-corrections-evidence`, exit 1, 26 passed and four failed with invalid-surrogate XML serialization errors. F1 Green: the identical command with `LogFileName=f1-green.trx`, exit 0, 30 passed, zero failed/skipped.
3. F2 Red: the preceding FieldTests command with `LogFileName=f2-red.trx`, exit 1, 30 passed and two failed: nested PAGE produced no exception, and nested MERGEFIELD produced InvalidOperationException instead of TemplateException. F2 Green: the identical command with `LogFileName=f2-green.trx`, exit 0, 32 passed, zero failed/skipped.
4. F3 Red: `dotnet test MonoReport.Tests/MonoReport.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~RegionTests --logger 'trx;LogFileName=f3-red.trx' --results-directory /private/tmp/feature001-corrections-evidence`, exit 1, ten passed and one failed with NullReferenceException instead of TemplateException. F3 Green: the identical command with `LogFileName=f3-green.trx`, exit 0, 11 passed, zero failed/skipped. Each production correction followed its actual failing run; subsequent expanded matrices are additional coverage, not original-defect Red evidence.
5. Focused compatibility/integration command: `dotnet test MonoReport.Tests/MonoReport.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FieldTests|FullyQualifiedName~RegionTests|FullyQualifiedName~MergeEngineTests|FullyQualifiedName~OoxmlEdgeTests|FullyQualifiedName~CliTests' --logger 'trx;LogFileName=focused-complete.trx' --results-directory /private/tmp/feature001-corrections-evidence`, exit 0, 190 passed, zero failed/skipped. The successful all-story test was subsequently strengthened with populated regions and verified in the final suite.
6. Final Release build: `dotnet build MonoReport.slnx -c Release --no-restore`, exit 0, zero warnings/errors. Final suite: `dotnet test MonoReport.slnx -c Release --no-build --no-restore --logger 'trx;LogFileName=feature001-corrections.trx' --results-directory /private/tmp/feature001-corrections-results`, exit 0, 215 passed, zero failed/skipped. Test counts are FieldTests 107, RegionTests 34, MergeEngineTests 14, OoxmlEdgeTests 12, CliTests 23, TemplateFormatterTests 9, and FormatterTests 16; 125 additional executed cases compared with the original baseline. The independent Reviewer separately rebuilt and executed all 215 tests, as recorded in section 13.
7. Actual CLI preparation: `dotnet build MonoReport.Cli/MonoReport.Cli.csproj -c Release --no-restore`, exit 0, zero warnings/errors. Exact plan sample smoke: `dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --json-file Samples/sample.json --output /private/tmp/feature001-corrections-sample.docx --culture en-GB`, exit 0 with empty stdout/stderr. The generated artifact passed Open XML validation and exact comparison with the separately inspected sample output.
8. Actual process smoke matrix: `python3 /private/tmp/feature001-cli-verifier/run-smokes.py /private/tmp/feature001-cli-fixtures/config.json`, exit 0, eight successful assertions covering sample and Unicode file generation plus both invalid kinds in stdout, new-file, and existing-file modes. Every exact built-CLI argument list, exit code, captured stderr, stdout byte count, and output path is recorded in `/private/tmp/feature001-cli-smokes-76fmo8ex/results.json`. All six rejection invocations exited 1, emitted zero stdout bytes and actionable diagnostics, created no new file, preserved existing sentinel files, and left template bytes unchanged.
9. Two additional actual CLI invocations re-merged `/private/tmp/feature001-cli-smokes-76fmo8ex/unicode-file.docx` into `unicode-second.docx`, then into `unicode-third.docx`, with `/private/tmp/feature001-cli-fixtures/second.json` and `third.json`, respectively. Each used the built CLI with `--template`, `--json-file`, `--output`, and `--culture en-GB`, exited 0, and produced empty stdout/stderr. Exact complete commands are retained in `/private/tmp/feature001-cli-smokes-76fmo8ex/repeat-commands.json`. Including item 7, the Orchestrator executed 11 built-CLI process checks; section 13 records three further independent Reviewer CLI invocations.
10. Independent standalone validator: `dotnet build /private/tmp/feature001-cli-verifier/verifier.csproj -c Release`, exit 0, zero warnings/errors; it references existing Open XML assemblies and does not change application code. Executed `dotnet /private/tmp/feature001-cli-verifier/bin/Release/net10.0/verifier.dll` with each exact DOCX path; zero schema errors were verified for all three independently constructed CLI fixtures, the sample template, three Unicode outputs, the sample smoke output, and the exact plan sample output. Every new successful automated-test output and negative fixture also receives whole-package OpenXmlValidator checks. Validation is separate from Word application verification.
11. Separate content and structure assertions verified exact Unicode values `😀A🚧B😀`, `🚧Second😀`, then `Concrete`; each text node has valid surrogate pairs. Original complex instruction text, begin/separate/end sequence, and complete bold/italic/underline run properties remain preserved. Begin-field dirty=false is the existing renderer behavior. The construction sample retains expected contractor/project/materials, dates/prices, 35 ordinary fields, eight columns and four rows including the header; technical markers are removed. Exact table properties, grid, row/cell/paragraph properties, material field instructions, and complete selected run properties match the template. Results are in `artifact-validation.json`, `structure-validation.json`, and `plan-sample-validation.json` in the smoke directory. The previously approved Russian RUB spell-out exception is unchanged.
12. Environmental and test-development limitations: sandbox MSBuild named-pipe and VSTest socket permission failures required approved execution outside the sandbox; these were not defect Red evidence or successful checks. An intermediate missing-import compile failure and expanded-test namespace-prefix/custom-switch fixture errors were corrected before the recorded passing runs. No separate solution restore command or additional package download was required. Plain `git diff --check` exited 2 because Git treats preserved CRLF additions as trailing whitespace; `git -c core.whitespace=cr-at-eol diff --check` exited 0. Original newline conventions were preserved instead of introducing unrelated normalization.
13. Scope preservation: original staged binary diff remains byte-for-byte unchanged; original specification bytes and historical review/plan prefixes are preserved. AGENTS.md, all four controlling Skills, and all three Samples files retain their starting hashes. The separate external backlog edit is preserved and excluded from this implementation. Changed correction files are CachedResultWriter, OoxmlFieldParser, RegionParser, DocumentRenderer, MergeEngine, FieldReference, focused test/helper files, and README diagnostics; workflow artifacts record approval/review/evidence only. No push, merge, publication, branch deletion, or commit was performed.

## 15. Acceptance handoff

1. Following the independent scoped PASS in section 13 and executed checks in section 14, Feature 001 enters acceptance under the Product Owner's explicit instruction. feature-status.json contains only phase=acceptance. The feature is not completed or accepted.
2. Manual desktop Microsoft Word verification remains pending for original requirement 147 and plan section 9.8. Human review should open `/private/tmp/feature001-corrections-sample.docx`, `/private/tmp/feature001-cli-smokes-76fmo8ex/unicode-file.docx`, and `unicode-second.docx` in the same directory, verify no repair dialog, exact readable values, material rows, formatting, and retained field codes, then record checker, Word version/platform, date, and outcome. Automated validation cannot substitute for this evidence.
3. Explicit Product Owner acceptance remains required before completion. Version-stage scope and collection re-merge questions from the historical audit remain outside the approved correction scope; no unrelated backlog functionality was implemented. Stop correction work after this handoff and await further instructions.

## 16. Product Owner manual Word verification and final acceptance — 2026-10-11

1. The Product Owner explicitly accepted Feature 001, version 0.0.1, in the request “Act as feature-orchestrator and finalize Feature 001”. This is final acceptance supplied by the Product Owner, separately from the independent Reviewer PASS; it supersedes the pending acceptance and manual-verification statements in the historical sections without rewriting their evidence.
2. Human Microsoft Word verification: the Product Owner confirmed that Samples/sample.docx opens correctly and that CLI document generation works. The Product Owner also confirmed that both `/private/tmp/feature001-word-verification/unicode-first.docx` and `/private/tmp/feature001-word-verification/unicode-second.docx` open correctly, display emoji, and preserve MERGEFIELD codes and formatting. The first contains `😀A🚧B😀`; the second is a re-merge with `🚧Second😀`. This supplies the previously pending human Word verification for original requirement 147 and the targeted F1 checks. The checker is the Product Owner; Word version and platform were not supplied and are not inferred.
3. Automated closure evidence was checked against the existing artifacts: independent implementation review section 13 records PASS with no blocking F1–F3 findings and independently executed Release build with zero warnings/errors. Developer and independent Reviewer full-suite TRX files each contain 215 passed, zero failed/skipped. F1, F2, and F3 Green TRX files contain 30, 32, and 11 passes, respectively; focused compatibility/integration verification contains 190 passes. Recorded CLI success/rejection, Open XML validation, exact Unicode values, retained field codes, and formatting evidence were verified. No build or test was newly executed for this documentation-only finalization; earlier executed results remain the evidence.
4. Feature 001 is completed following verified independent review, automated checks, confirmed human Word verification, and explicit Product Owner acceptance. feature-status.json contains exactly {"phase": "completed"}. No acceptance check remains pending for this finalization; no historical requirement classification or approval is fabricated.
5. Deferred functionality remains in the existing backlog: conditional rendering, HTTP API, Word Add-in, extended formatting/currencies, exploratory HTML/PDF output, and C# style/automated-formatting improvements. These require their own future approval and workflow. Approved nested cached-result field and conflicting independent-region rejection policies remain supported behavior; rendered collections still require the original template for expansion. No additional collection re-merge contract or future-stage scope is introduced by this closure.
6. All prior review sections and evidence are preserved. This finalization changes only review.md and feature-status.json; existing staged changes, application code, tests, specifications, committed samples, and other documentation are preserved. No push, merge, or publication was performed.
