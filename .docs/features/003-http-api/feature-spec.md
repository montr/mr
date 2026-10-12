# Feature 003 — MonoReport HTTP API

## 1. Document Status and Goal

1. The complete Feature Spec, the CLI scope amendment in Section 13, and the documentation-exposure amendment in Section 14 were explicitly approved by the Product Owner on 2026-10-12. The independently re-reviewed implementation plan was also approved before the Section 14 amendment. Independent implementation review and explicit Product Owner acceptance remain required.

2. Expose existing DOCX generation and independent structural template validation through two synchronous HTTP endpoints for external applications, with generated OpenAPI and interactive documentation, and expose the same Core validation capability through an additive CLI validation mode.

3. Prior Product Owner decisions remain agreed. The requested validation endpoint and additive Core capability extend the earlier one-endpoint scope. Section 11 records the agreed architecture; Section 12 records decision approvals separately from approval of the complete specification.

## 2. Existing Implementation and Architectural Evidence

4. `MergeEngine.Merge` copies the template, opens the DOCX, loads main document, headers, footers, footnotes, and endnotes, preflights every loaded story before rendering, and returns independent document bytes.

5. `DocumentRenderer.Preflight` uses `OoxmlFieldParser.Validate`, field parsing, and `RegionParser.Validate`. Existing rules cover field syntax/structure, collection marker pairing, supported boundaries, nesting, region overlap, and unsupported Word fields inside MERGEFIELD cached results.

6. Current parsers throw the first `TemplateException`. That exception carries optional field, field-code, formatter, and location context, but no stable issue code. Assign codes at rule detection or through typed diagnostics. Neither endpoint nor Core validation may derive diagnostic codes or classifications by parsing exception messages.

7. `AddMonoReport()` supplies transient processing services with singleton registry/built-in formatter; operation data is independent. Registry configuration must finish before concurrent use.

8. `MergeOptions.Culture` and the CLI already default to invariant culture. Preserve the existing formatting mechanism.

## 3. Scope and Out of Scope

9. Add mandatory separate executable ASP.NET Core project `MonoReport.Api`, targeting .NET 10, included in `MonoReport.slnx`, and referencing `MonoReport`. Core and CLI must not reference the API project or acquire HTTP dependencies.

10. Provide `POST /api/documents/generate`, `POST /api/documents/validate`, CLI `--validate` mode, a public Core structural validator, shared structural rules, safe errors, generated OpenAPI, Scalar, automated verification, and README guidance. The CLI amendment is explicitly approved as recorded in Section 13.

11. Validation is independent of generation: no trial merge, dry run, JSON requirement, culture input, formatter execution, generated DOCX, or `dryRun` flag.

12. Exclude authentication/authorization, persistence, template management, background jobs/queues, asynchronous APIs, XLSX, MCP, separate end-user UI, additional endpoints, and unrelated refactoring.

13. Exclude exhaustive schema auditing of every OOXML feature, template repair, arbitrary parser recovery, and unsupported document formats. Package validation means readable supported DOCX structure and supported stories, not universal Word compatibility certification.

14. Cloud manifests, container images, proxy configuration, TLS certificate management, and publication are outside scope; deployment protections must still be documented.

## 4. Generation HTTP Contract

15. `POST /api/documents/generate` consumes only `multipart/form-data`: required DOCX file `template`, required text field `data` containing raw JSON, and optional text field `culture`.

16. Parse `data` exactly once as one JSON value using runtime default maximum depth. Reject empty/malformed/trailing-content JSON and a file part supplied instead of the required text field.

17. Omitted, empty, or whitespace-only culture uses `InvariantCulture`; explicit nonempty culture uses `CultureInfo.GetCultureInfo` through `MergeOptions.Culture`. Reject invalid culture and ignore `Accept-Language` and ambient server culture.

18. Reject missing, empty, duplicate, or incorrectly represented known inputs. Unknown generation fields may be ignored. Submitted filenames are metadata and never server paths.

19. Return HTTP `200` with raw DOCX bytes, standard DOCX media type, and attachment disposition with safe deterministic filename such as `document.docx`. No response envelope or redundant success flag.

20. Include `X-MonoReport-Warning-Count` only when warnings exist. Preserve default missing-value behavior; detailed warning transport is outside scope.

21. Invalid DOCX/structural template defects and data-dependent rendering incompatibilities produce `422`, with distinct machine codes. Return no partial document.

## 5. Validation HTTP Contract

22. `POST /api/documents/validate` accepts exactly one required nonempty DOCX file `template` in multipart form data. Reject `data`, `culture`, other additional inputs, duplicate templates, and text-only templates as request errors.

23. Invoke the Core structural validator directly without merging, evaluating data/formatters, or creating output DOCX bytes.

24. Completed validation returns HTTP `200` and `application/json`, including structural errors, with `valid`, `errors`, and `warnings`. Both issue collections are always present.

25. Each issue has stable machine-readable `code`, safe human-readable `message`, and nullable `field`, `part`, and `location`. Part identifies a package part URI; location identifies document context, never a filesystem path. Arbitrary internal messages and raw field-code text are not response requirements.

26. `valid` is true if and only if `errors` is empty. Warnings do not invalidate a template. Structural validity does not guarantee successful rendering with arbitrary future data.

27. No new heuristic warning rules are required. Return an empty warnings collection unless a defined nonfatal structural rule supplies an issue.

28. Nonempty corrupt/non-DOCX content returns `200`, `valid=false`, and `template.invalid_package` or `template.unsupported_structure`. Missing/empty/malformed request inputs are `400`; unexpected operational I/O or server failure is `500`.

29. Package corruption can prevent story discovery: report the package issue without fabricated field errors. Malformed XML in an independently readable story produces `template.invalid_xml`, with other readable stories checked when reliable.

## 6. Core Validator and Shared Validation

30. Provide injected public instance `TemplateValidator.Validate(Stream template)` returning structured `TemplateValidationResult`, with read-only `TemplateValidationIssue` error/warning collections. Require no JSON, culture, merge options, formatter, or HTTP dependency.

31. The caller owns the stream. Read from its current position without altering content or closing it; position can advance. Support readable non-seekable streams through owned buffering where necessary and dispose all owned resources.

32. Null/unreadable Core arguments throw appropriate argument exceptions. Expected template defects become issues, while unexpected operational failures remain exceptions.

33. Register validator and shared structural collaborators through `AddMonoReport()`, preserving override/repeated-registration behavior. Use external constructor injection and per-operation context.

34. Reuse supported-story discovery, safe XML loading, field parser rules, region pairing/boundary/overlap rules, and nested-field restrictions in focused DOCX-specific components. Avoid speculative format abstractions.

35. Generation and standalone validation must call the same structural rule implementations. Both API and CLI standalone validation directly call the same injected public Core validator. Generation validates before rendering and preserves existing Core/CLI generation exception semantics and useful diagnostics. Reuse already-loaded stories rather than performing a redundant package traversal to call the public stream API.

36. Check main document, every supported header/footer, footnotes, and endnotes for package/part failures, malformed MERGEFIELD syntax, invalid complex-field structure, collection markers/nesting, unsupported boundaries, conflicting regions, and unsupported nested fields.

37. Do not check JSON property existence, values/types, collection data, runtime formatter availability/evaluation, or culture formatting. Format syntax checked by existing parsers remains structural; value/formatter failures detected only while rendering remain generation errors.

38. Collect independently detectable structural errors across all independently readable stories and safe validation stages after trustworthy field/region parsing. Stop a dependent stage when malformed stacks or ambiguous marker nesting prevent safe interpretation; continue other stories. Do not attempt unreliable recovery from corrupted parser state.

39. Do not promise exhaustive detection of every defect in a damaged story or implement guessed token recovery. Existing fail-fast stages may yield one error for an affected story/stage; record this limitation explicitly and test it.

40. Use deterministic story/document order with documented tie-breakers, avoid duplicate reports of the same failure, and permit null field/location where unavailable.

## 7. Error Taxonomy and Required Cases

41. Application failures use RFC 9457 `application/problem+json` with `type`, `title`, `status`, safe `detail`, stable extension `code`, and `traceId`; safe field context may be included. Never expose stacks, server paths, raw package exceptions, payloads, or document bytes.

42. Both endpoints: missing/empty/duplicate/wrongly represented input returns `400` with `request.missing_input`, `request.empty_input`, `request.duplicate_input`, or `request.invalid_input`. HTTP tests cover each applicable category.

43. Both endpoints: malformed multipart/missing boundary returns `400` with `request.invalid_multipart`; validation rejects extra inputs with `400` and `request.unexpected_input`. Test data, culture, unknown inputs, and duplicate uploads.

44. Both endpoints: whole-request limit violation returns `413` with `request.too_large`; wrong overall media type returns `415` with `request.unsupported_media_type`. Test each endpoint with a small configured limit and actual request body.

45. Generation: malformed/trailing/depth-exceeding JSON returns `400` with `data.invalid_json`; invalid culture returns `400` with `culture.invalid`. Include tests for every category and safe messages.

46. Generation: invalid package, malformed story XML, or unsupported DOCX structure returns `422` with `template.invalid_package`, `template.invalid_xml`, or `template.unsupported_structure`. Test invalid bytes, missing main structure, and malformed part XML.

47. Structural categories are `template.invalid_field_syntax`, `template.invalid_field_structure`, `template.invalid_region`, `template.overlapping_regions`, and `template.unsupported_nested_field`. Test each in Core and validation HTTP; generation maps representative shared-rule failures to `422` Problem Details.

48. Generation data/formatting failures return `422` with `generation.invalid_data`, `generation.invalid_format`, or `generation.unknown_formatter`. Test collection-as-scalar, incompatible scalar object/array, formatting failure, and unknown formatter, preserving established missing-value behavior.

49. Both endpoints: unexpected operational failures return `500` with `server.internal_error`. Test sanitization and recovery. Do not broadly classify every I/O, argument, or invalid-operation exception as bad client input.

50. Validation structural defects, including nonempty invalid DOCX package content, return `200`, `valid=false`, and structured issues rather than Problem Details. Request input failures remain `400` and unexpected server failures remain `500`; validation has no `422` contract. Generation retains `422` for invalid templates or incompatible data.

51. Framework errors are normalized where application handling is possible. A host/proxy rejection before application execution may use a host-owned response; document and verify this boundary with a real host where test hosting bypasses size enforcement.

52. Codes/status mappings and contracts must appear in generated OpenAPI, README, and tests. CLI validation uses the same issue codes and JSON result contract; CLI argument and execution failures use stderr and the exit codes in Section 13, without HTTP Problem Details. Message wording may evolve while code identity remains stable.

## 8. Resources and Hosting

53. Use one configurable 50 MiB whole-request size limit for both endpoints. Align standard host/form buffering configuration so incidental lower per-field defaults do not become unintended payload limits; retain ordinary multipart parsing safeguards.

54. Add no separate template, JSON, or output limits, custom generation timeout, application concurrency limiter/queue, or `429` response. Rely on standard ASP.NET Core hosting/buffering and avoid a custom temporary-file subsystem.

55. Dispose application-owned streams, packages, JSON, and contexts correctly; retain framework ownership of request buffering and temporary resources. Cancellation stops reading/writing where possible without claiming hard cancellation of executing synchronous Core merges or validation.

56. Concurrent operations use independent input/context and shared configuration without mutation. No custom request admission policy is added.

57. The compressed request bound does not bound decompressed content, expansion, output, runtime, or aggregate concurrent memory. Document public-deployment TLS, edge traffic/connection controls, access controls, operational timeouts, capacity, and monitoring outside this feature.

## 9. OpenAPI and Interactive Documentation

58. Use Minimal APIs; Controllers are a fallback only for correct multipart OpenAPI support. Runtime adapters remain injected instances in `MonoReport.Api`. Resolve engine and validator through `AddMonoReport()`.

59. Generate OpenAPI with .NET 10 `Microsoft.AspNetCore.OpenApi` for both endpoints. Document multipart requirements, generation JSON/culture and binary response/conditional warning header, validation template-only result/issues, errors and machine codes.

60. Generation documents `200`, `400`, `413`, `415`, `422`, `500`; validation documents JSON `200`, `400`, `413`, `415`, `500`, including valid/invalid results. No authentication scheme.

61. Use Scalar with Swagger UI fallback only if complete workflows cannot be demonstrated. Real-browser verification covers generation upload/JSON/culture/download and validation upload/execute/display structured valid/invalid diagnostics.

62. Enable OpenAPI JSON and the interactive UI by default in every environment, including Development and Production. Permit an explicit configuration override to disable or re-enable documentation without changing the environment. Intended routes are `/openapi/v1.json` and `/docs`, subject to supported package routing.

63. Assert actual generated schemas, required properties, binary/JSON media types, statuses, context/nullability, default exposure in Development and non-Development environments, and explicit enable/disable configuration overrides. Merely loading the UI is insufficient.

## 10. Verification, README, and Acceptance

64. Core tests cover all supported stories and structural categories, valid/nested regions, package/part failures, stream ownership/non-seekable input, deterministic validity/order, and concurrent independent validation.

65. Completeness tests exercise independent defects in separate stories, safely detectable same-story errors, unsafe stack/nesting boundaries, unavailable context, and absence of guessed cascading diagnostics. Match the agreed aggregation guarantee.

66. Shared-rule tests compare validation and generation structural acceptance/rejection. Verify stable diagnostic codes originate from typed diagnostics or rule detection and remain independent of exception-message wording. Prove standalone validation does not merge or evaluate JSON/formatters. Preserve and run the existing Core/CLI regression suite.

67. Actual HTTP multipart tests cover both contracts and every applicable error category. Generated DOCX must pass Open XML validation and content assertions; culture tests cover explicit cultures, invariant omission/blank, invalid names, and ignored Accept-Language.

68. Validate cleanup/recovery, concurrent isolation, and the one whole-request boundary for both endpoints. Verify generated OpenAPI, UI smoke loading, and recorded real-browser workflows.

69. README lists the API project, run prerequisites/commands, always-on-by-default documentation routes and configuration override, both contracts, concise copyable HTTP examples, Core validation usage/ownership, diagnostics/limitations, culture, 50 MiB HTTP setting, and deployment protections. Add concise CLI validation examples, accepted arguments, JSON/stderr behavior, and exit codes while retaining existing generation guidance. The HTTP request-size policy does not introduce a CLI file-size limit.

70. Acceptance requires the mandatory one-way API project boundary, raw generation DOCX and safe error behavior, independent template-only validation through HTTP and CLI, shared structural rules and agreed diagnostic scope, deterministic matching JSON result contracts, CLI argument/stream/exit behavior, preserved CLI generation, correct disposal/isolation, complete OpenAPI/UI workflows, tests, and accurate README.

71. Acceptance must not imply full OOXML certification or generation success with arbitrary data. Required implementation checks must actually run; this documentation update claims no implementation test PASS.

72. The specification, architecture decisions, CLI scope amendment, documentation-exposure amendment, and the plan as independently re-reviewed before the documentation-exposure amendment are approved. Implementation is authorized; the plan amendment in Section 11 of the Implementation Plan has not received a new independent review because the Product Owner explicitly postponed implementation review. Verified independent implementation review and explicit Product Owner acceptance remain required.

## 11. Agreed Decisions

73. Agreed previously: generation raw JSON text, culture invariant/explicit override and ignored Accept-Language; Minimal API/Controller fallback; Scalar/Swagger workflow fallback; raw DOCX/conditional warning count; generation 400/422 and Problem Details; mandatory separate API project; single 50 MiB limit and simplified resources; documentation enabled by default in every environment with an explicit configuration override; integration/Open XML/OpenAPI/browser/README checks.

74. Newly required: template-only validation endpoint; reusable public Core validator and shared rules; data-independent structural coverage; structured stable issues; practical diagnostic collection and complete taxonomy/tests/docs. These extend only the earlier one-endpoint restriction.

75. Decision A — approved by the Product Owner on 2026-10-12: injected public instance `TemplateValidator.Validate(Stream)` returns structured results with read-only diagnostic collections and caller-owned input stream, with the exception semantics stated above. This supports diagnostic aggregation while keeping HTTP contracts outside Core.

76. Decision B — approved by the Product Owner on 2026-10-12: collect independently detectable errors across readable document parts and safe validation stages, allowing one error in unsafe parser stages and stopping dependent recovery. Do not attempt unreliable recovery from corrupted parser state or promise exhaustive diagnostics. This captures independent defects without speculative parser redesign or misleading findings.

77. Decision C — approved by the Product Owner on 2026-10-12: invalid nonempty DOCX content returns validation HTTP `200` with `valid=false`; malformed/missing request inputs return `400`, unexpected server failures return `500`, and generation failures caused by invalid templates or incompatible data retain `422`. Expected content defects belong to validation diagnostics even when package corruption prevents further inspection.

78. The public validator contract, structural issue categories, shared rules, and prohibition on exception-message-derived codes remain approved requirements. The CLI amendment and re-reviewed plan are explicitly approved; routine internal details follow the approved plan.

## 12. Approval Record

79. Previously agreed decisions and the requested validation expansion are preserved. On 2026-10-12, the Product Owner explicitly approved all three pending architecture decisions recorded in Section 11, items 75–77, and required shared structural rule implementations and diagnostic codes independent of exception-message parsing. These decision approvals do not constitute approval of the complete Feature Spec.

80. On 2026-10-12, the Product Owner explicitly stated: “I approve the complete Feature 003 Specification.” The Orchestrator recorded this approval and transitioned Feature 003 to `planning`. Implementation planning and independent plan review are authorized; implementation must not begin until the Product Owner explicitly approves the reviewed implementation plan.

## 13. Approved CLI Scope Amendment

81. On 2026-10-12, the Product Owner explicitly approved the Feature 003 CLI validation scope amendment recorded in this section and the independently re-reviewed Implementation Plan including that amendment. The Orchestrator recorded both approvals and transitioned Feature 003 to `implementation`. Completion still requires independent implementation review, verified checks, and explicit Product Owner acceptance.

82. Add the value-less `--validate` flag to the existing CLI. In validation mode require exactly one `--template` path, preserving its existing `-t` alias. Reject JSON sources (`--json`, `--json-file`, `--stdin`), `--culture`, `--output`/`-o`, unknown or positional arguments, duplicate flags/options, missing template, and missing/blank values with exit `2`, an operational diagnostic on stderr, and no validation JSON. Preserve existing `--help`/`-h` precedence and exit `0`; update help to describe validation mode.

83. Resolve the same public `TemplateValidator` through application composition and constructor injection, open the specified template locally, and call `Validate(Stream)` directly. Do not call HTTP, perform a merge, evaluate formatters, require/read JSON or stdin, write output DOCX, or add a dry-run path. The CLI owns and disposes its opened stream according to the Core ownership contract.

84. For completed validation, write one UTF-8 JSON result to stdout with exactly the contract of validation HTTP `200`: `valid`, `errors`, `warnings`, and the same issue properties, nullability, codes, safe messages, and deterministic ordering. Exit `0` when valid and `1` when invalid. Template issues belong in the JSON collections; operational diagnostics belong on stderr. Serialize the Core result consistently in both adapters without introducing HTTP dependencies into Core or CLI.

85. Nonempty invalid DOCX content, including corrupt packages, is completed invalid validation: JSON `valid=false` on stdout and exit `1`. A readable empty file likewise uses the Core invalid-package diagnostic and exit `1`; HTTP rejection of an empty upload remains a request-boundary rule. Missing/unreadable files, permission errors, unexpected validation failures, or output failures return exit `1` with an operational diagnostic on stderr, without fabricating a completed validation result. Failures before JSON output begins leave stdout empty; an output-write failure may interrupt JSON and remains an execution failure. Keep diagnostic issue codes independent of exception-message parsing.

86. Preserve existing CLI generation arguments, default stdin JSON behavior, culture semantics, binary stdout/file output, help behavior, diagnostics, and exit codes when `--validate` is absent. Retain existing CLI regression coverage. Core and CLI continue to have no dependency on `MonoReport.Api` or HTTP packages.

87. Add parser/application tests and actual CLI process tests for valid templates (exit `0`), structurally invalid/corrupt/empty templates (exit `1` and JSON), independent diagnostic collection, every forbidden argument class and duplicate/missing inputs (exit `2`), file/execution failures (exit `1` and stderr), help, no stdin consumption, clean stdout/stderr separation, stream disposal, and unchanged generation. Compare CLI and HTTP validation results for the same fixtures, including property shape/nullability and codes, and verify CLI invokes Core validation without merging. README examples must cover CLI valid/invalid results and exit handling alongside both HTTP endpoints.

## 14. Approved Documentation-Exposure Amendment

88. On 2026-10-12, the Product Owner explicitly approved changing the documentation default: generated OpenAPI JSON and the interactive Swagger UI must be enabled by default in every environment, rather than only in Development. This approval authorizes the corresponding specification, plan, implementation, test, and README changes.

89. Preserve the existing `MonoReport:DocumentationEnabled` configuration setting as an explicit operational override. When the setting is absent, both `/openapi/v1.json` and `/docs` are exposed regardless of the ASP.NET Core environment; an explicit `false` disables both routes and an explicit `true` enables both routes.

90. This amendment does not alter either document endpoint, the absence of an authentication scheme, or the previously documented public-deployment protections. The Product Owner explicitly postponed independent implementation review; this amendment records requirement approval only and does not claim plan re-review, implementation verification, reviewer acceptance, or feature acceptance.
