# MonoReport

MonoReport generates DOCX documents from Microsoft Word templates and JSON data. It preserves Word fields and formatting, repeats collection regions, and supports custom value formatters. It requires .NET SDK 10; Microsoft Word is needed to edit or inspect templates, not to run the engine.

1. `MonoReport` is the library.
2. `MonoReport.Cli` is the command-line application and `monoreport` .NET tool package.
3. `MonoReport.Api` is the separate ASP.NET Core HTTP application. It references Core; Core and CLI have no HTTP dependencies.
4. `MonoReport.Tests` contains Core and Open XML tests.
5. `MonoReport.Cli.Tests` contains CLI service and real-process tests.
6. `MonoReport.Api.Tests` contains HTTP integration, generated OpenAPI contract, and real Kestrel host tests.

Build and test from the repository root. These single-line commands work in macOS/Linux Bash or zsh, Windows PowerShell, and Windows CMD:

```bash
dotnet build MonoReport.slnx -c Release
dotnet test MonoReport.slnx -c Release
```

The normal solution test command runs Core, CLI, and `WebApplicationFactory` HTTP integration tests, including generated OpenAPI contract coverage. `MonoReport.Api.Tests` automatically excludes the `RealHost` category because those tests start real Kestrel processes. Run that category explicitly when needed. On macOS/Linux Bash or zsh:

```bash
dotnet test MonoReport.Api.Tests/MonoReport.Api.Tests.csproj -c Release \
  -p:VSTestTestCaseFilter=Category=RealHost
```

On Windows PowerShell:

```powershell
dotnet test MonoReport.Api.Tests/MonoReport.Api.Tests.csproj -c Release `
  -p:VSTestTestCaseFilter=Category=RealHost
```

Browser/Playwright verification is a separate manual workflow and is not part of the normal local test run.

![Word template, JSON data, and generated construction estimate](.docs/Template%20to%20JSON%20to%20Document%20Workflow.png)

This illustration shows the workflow conceptually. For working templates, use the actual Word MERGEFIELD syntax and item-relative paths described in [Collections](#collections); the pictured field notation and JSON are illustrative.

## Core API

Inject `MergeEngine` into your application's document service. This example opens fresh inputs for each generation and returns an independent document with any warnings. The caller decides where to save it.

```csharp
using System.Text.Json;
using MonoReport.Models;
using MonoReport.Services;

public sealed class DocumentService(MergeEngine engine)
{
    private readonly MergeEngine _engine = engine
        ?? throw new ArgumentNullException(nameof(engine));

    public MergeResult Generate(
        string templatePath, string json, MergeOptions? options = null)
    {
        using var template = File.OpenRead(templatePath);
        using var data = JsonDocument.Parse(json);
        return _engine.Merge(template, data, options);
    }
}
```

`MergeEngine.Merge` reads a stream from its current position without modifying or closing it. The caller owns both input objects. It processes the main document, headers, footers, footnotes, and endnotes. Template and formatting errors throw `TemplateException`, with field, code, and document-location details where available.

### Structural template validation

Resolve the injected public `TemplateValidator` through `AddMonoReport()` and call `Validate(Stream template)`. It reads from the stream's current position, supports readable non-seekable streams, does not modify the content, and leaves the caller-owned input open; its position may advance. Dispose the input yourself. The result provides `Valid` and read-only `Errors` and `Warnings` snapshots. Null/unreadable arguments throw argument exceptions; operational I/O failures remain exceptions; invalid or empty DOCX content produces an invalid result.

Validation checks DOCX package and supported story structure, MERGEFIELD syntax and Word field structure, collection marker pairing/nesting/boundaries/overlap, and unsupported fields in cached results. It uses the same structural rules as generation without merging, requiring JSON/culture, evaluating formatters, or generating document bytes. It does not check data paths, types, or whether future generation will succeed, and is not universal OOXML certification.

Issues have stable `code`, safe `message`, and explicit nullable `field`, `part`, and `location`. `part` is a package URI, never a server file path. `valid` is true exactly when `errors` is empty; warnings do not invalidate a template. No heuristic warnings are added. Diagnostics follow main document, ordinal URI order of headers, ordinal URI order of footers, footnotes, then endnotes, with document order within safe stages. Independent errors are collected across readable stories and trustworthy stages. An unsafe parser stack or ambiguous markers stop dependent checks for that story; damaged packages can prevent story discovery. Exhaustive diagnostics and guessed recovery are not promised.

## HTTP API

Build from the repository root with .NET SDK 10 and start the separate host. On macOS/Linux Bash or zsh:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5080 \
  dotnet run --project MonoReport.Api/MonoReport.Api.csproj -c Release --no-build
```

On Windows PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet run --project MonoReport.Api/MonoReport.Api.csproj -c Release --no-build
```

OpenAPI JSON is at `http://127.0.0.1:5080/openapi/v1.json`; Swagger UI interactive documentation is at `http://127.0.0.1:5080/docs`. Both are enabled by default in every environment after an ordinary API start. Set `MonoReport__DocumentationEnabled=false` to disable both routes or `true` to enable them explicitly, independently of the environment. The host supplies no authentication scheme. Scalar was evaluated first, but its multipart client did not produce a valid upload request with the selected generated contract, so Feature 003 uses the approved Swagger UI fallback.

1. `POST /api/documents/generate` accepts `multipart/form-data` with one required nonempty DOCX file `template`, one required text field `data` containing a single raw JSON value, and optional text `culture`. Omitted/blank culture means invariant; an explicit valid culture overrides it. `Accept-Language` is ignored. Duplicate or wrongly represented known inputs fail; unknown generation inputs are ignored. Submitted filenames never become server paths.
2. Generation success returns HTTP 200 with raw DOCX bytes, DOCX media type, and attachment filename `document.docx`. `X-MonoReport-Warning-Count` exists only when warnings exist; the default missing-value policy remains empty without warnings. There is no JSON response envelope or success flag.
3. `POST /api/documents/validate` accepts exactly one nonempty DOCX file `template`. Additional inputs, including data and culture, fail. It directly calls Core validation. Completed validation returns HTTP 200 JSON even for invalid nonempty DOCX content, with `valid`, `errors`, and `warnings`. Both arrays and all nullable issue properties are always present. Empty uploads are request errors.

Generate the sample and save its raw response on macOS/Linux Bash or zsh. `-o` writes the binary DOCX directly to a file:

```bash
curl --fail-with-body http://127.0.0.1:5080/api/documents/generate \
  -F 'template=@Samples/sample.template.docx' \
  --form-string "data=$(cat Samples/sample.json)" \
  --form-string 'culture=en-GB' -o /tmp/monoreport-generated.docx
```

Validate the sample without JSON or culture on macOS/Linux Bash or zsh:

```bash
curl --fail-with-body http://127.0.0.1:5080/api/documents/validate \
  -F 'template=@Samples/sample.template.docx'
```

### Error contracts

HTTP application errors use RFC 9457 `application/problem+json`: `type`, `title`, `status`, safe `detail`, machine-readable `code`, and `traceId`; optional safe field context may appear. They never return exception stacks, server paths, raw field instructions, input data, or partial generated documents. Stable code identity is independent of message wording.

1. Both endpoints, HTTP 400: missing, empty, duplicate, wrongly represented inputs use `request.missing_input`, `request.empty_input`, `request.duplicate_input`, `request.invalid_input`; malformed multipart or missing boundary uses `request.invalid_multipart`. Validation extra inputs use `request.unexpected_input`.
2. Both endpoints, HTTP 413: `request.too_large`. HTTP 415: `request.unsupported_media_type` for an overall media type other than multipart/form-data.
3. Generation, HTTP 400: malformed, trailing-content, or depth-exceeding JSON uses `data.invalid_json`; invalid culture uses `culture.invalid`.
4. Generation, HTTP 422: unreadable/corrupt DOCX uses `template.invalid_package`; malformed story XML uses `template.invalid_xml`; missing/unsupported story structure uses `template.unsupported_structure`.
5. Shared structural codes: `template.invalid_field_syntax`, `template.invalid_field_structure`, `template.invalid_region`, `template.overlapping_regions`, and `template.unsupported_nested_field`. Generation returns 422 Problem Details; validation returns 200 with `valid=false` and structured errors, also for the package/XML/structure codes above. Validation never returns 422.
6. Generation, HTTP 422: incompatible collection/scalar data uses `generation.invalid_data`; failed numeric/date/custom formatting uses `generation.invalid_format`; unavailable formatter uses `generation.unknown_formatter`.
7. Both endpoints, HTTP 500: unexpected operational failures use `server.internal_error`. These are server failures rather than invalid-template results. A host or reverse proxy rejecting a request before application handling can return its own response instead of application Problem Details.

### Resource policy and deployment

The default whole HTTP request limit is 50 MiB (52,428,800 bytes), including multipart framing and every part. Configure the single positive `MonoReport__MaxRequestBytes` setting (maximum `Int32.MaxValue`) before host startup. Standard Kestrel/form handling enforces the boundary; there are no separate template, JSON, output, generation timeout, application concurrency limiter, queue, or 429 policies. The application disposes its input/JSON/package resources; ASP.NET Core owns request buffering and temporary resources. CLI file validation has no HTTP size limit.

This compressed request bound does not bound decompressed content, generated output, processing time, or aggregate concurrent memory. Synchronous Core merging/validation cannot be hard-cancelled after execution begins; asynchronous transport honors cancellation where possible. Public deployments need infrastructure TLS, access controls, edge request/connection/rate protections, operational timeouts, capacity planning, and monitoring. Provision these at the host/proxy boundary; this repository does not supply deployment manifests or authentication infrastructure.

For example, this application method receives `DocumentService` from its caller and saves the construction estimate:

```csharp
void SaveEstimate(DocumentService documents)
{
    var json = File.ReadAllText("Samples/sample.json");
    var result = documents.Generate("Samples/sample.template.docx", json);
    File.WriteAllBytes("result.docx", result.Document);
}
```

### Dependency injection

Add these registrations to the `IServiceCollection services` supplied by your application's host or composition root:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddMonoReport();
services.AddTransient<DocumentService>();
```

Standalone applications using Microsoft's provider need the `Microsoft.Extensions.DependencyInjection` package alongside `MonoReport`. The host or composition root owns provider creation and disposal. Default engine services are transient, and the formatter registry and built-in formatter are singleton within the provider; no scope is needed for the default graph. Registration preserves consumer overrides and can be repeated without duplicating defaults. Manual constructor composition remains supported as an alternative.

## Fields and formatting

Insert a real Word field through **Insert → Quick Parts → Field → MergeField**. Typing braces and field text does not create a field. For example:

```text
{ MERGEFIELD Contractor.Name \* MERGEFORMAT }
{ MERGEFIELD Totals.Amount \# "#,##0.00" }
{ MERGEFIELD DeliveryDate \@ "dd MMM yyyy" }
```

Simple and complex Word fields are supported, including split instructions and cached results. Field codes and existing run formatting remain intact. Cached text is replaced across the existing runs; Unicode characters remain valid at run boundaries. Newlines and tabs become Word breaks and tabs. Populated fields are not marked as needing an update.

1. `\* Upper`, `Lower`, `Caps`, and `FirstCap` transform text; `MERGEFORMAT` preserves formatting.
2. `\#` applies a .NET numeric format, such as `#,##0.00`.
3. `\@` applies a .NET date format, such as `dd MMM yyyy`; use ISO 8601 dates in JSON.
4. Switches may be combined and run in their specified order. The complete historical Word picture-format grammar is not implemented.

JSON property names are case-sensitive; dotted paths resolve nested properties. JSON `null` produces empty text. Objects and arrays cannot be inserted as scalar values. Missing fields are empty by default; warning and error policies are available. The default numeric/date culture is invariant.

This optional block goes inside `SaveEstimate`, after the JSON is read. Its `documents` parameter and `json` local variable come from that method. `Generate` opens a fresh template for each call:

```csharp
var optionsResult = documents.Generate(
    "Samples/sample.template.docx", json,
    new MonoReport.Models.MergeOptions
    {
        Culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
        MissingValues = MonoReport.Models.MissingValueBehavior.Warning
    });
foreach (var warning in optionsResult.Warnings)
    Console.WriteLine(warning.Message);
File.WriteAllBytes("result-en-GB.docx", optionsResult.Document);
```

To update ordinary fields again, merge the generated bytes through a new `MemoryStream` with new data. Field codes and formatting remain available for the next merge.

## Collections

Use MERGEFIELD markers around the rows to repeat. Keep the table header outside the region:

```text
Material                            | Quantity | Unit price
{ MERGEFIELD TableStart:Items }      ← separate marker row
{ MERGEFIELD Name }                  | { MERGEFIELD Quantity } | { MERGEFIELD Price \# "0.00" }
{ MERGEFIELD TableEnd:Items }         ← separate marker row
```

```json
{"Items":[{"Name":"Portland cement (25 kg bag)","Quantity":100,"Price":500},{"Name":"Concrete blocks (440 x 215 x 100 mm)","Quantity":250,"Price":261.722}]}
```

1. The engine repeats the region for each item; an empty array produces no data rows. It removes markers and rows or paragraphs containing only markers, while retaining ordinary fields and the outside header.
2. Inside a region, use paths relative to the current item, such as `Name`, not `Items.Name`. There is no implicit lookup in the parent object. Nested collections restore the enclosing item's context when the inner region ends.
3. Put boundaries in rows of the same table or paragraphs sharing a parent. Both markers may surround fields in one data row. For nested regions, use separate boundary rows or paragraphs. Ranges across tables or document parts are unsupported.
4. Independent regions must have separate, nonoverlapping paragraph or row ranges. Shared blocks, including overlaps through nested tables, are rejected; genuine parent/child regions are supported.
5. Re-expanding a collection requires the original template. Generated documents no longer contain region markers or row-to-array bindings; re-merging them updates ordinary fields without reconstructing collections.

Nested Word fields in a MERGEFIELD's cached result are unsupported. Move them outside that result or replace it with plain cached text. Instruction nesting and independent sibling Word fields keep their supported behavior. The engine validates all processed document parts before rendering; invalid layouts produce actionable errors without partial CLI output.

## Custom formatters

To request an amount in words in the template, display the field code with **Shift+F9** and add `\* SpellOut`:

```text
{ MERGEFIELD Totals.Amount \* SpellOut }
{ MERGEFIELD Totals.Amount \* SpellOut \* MERGEFORMAT }
```

`SpellOut` is a MonoReport extension. It generates Russian RUB wording regardless of numeric/date culture: `125430.50` becomes `Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек`. It supports grammatical inflection, negative values, and rounding to kopecks away from zero; the rounded absolute amount must be below 10¹⁸ RUB. Formatter names are case-insensitive.

Alternatively, the following optional block also goes inside `SaveEstimate` and binds the built-in formatter programmatically. Create `formatter-template.docx` with an ordinary `{ MERGEFIELD Amount }` field, without a numeric `\#` switch: a programmatic formatter runs before field switches, and its words cannot be processed as a number.

```csharp
var formatterResult = documents.Generate(
    "formatter-template.docx", """{"Amount":125430.50}""",
    new MonoReport.Models.MergeOptions
    {
        FieldFormatters = new Dictionary<string, MonoReport.Models.FormatterBinding>
        {
            ["Amount"] = new("spellOut", new Dictionary<string, string>
            {
                ["currency"] = "RUB"
            })
        }
    });
File.WriteAllBytes("amount-in-words.docx", formatterResult.Document);
```

For other transformations, implement `IValueFormatter` and use the existing `FormatterRegistry.Register` method. Resolve the shared registry from the provider and finish registration before the first merge and before concurrent use. Bindings use field names, including item-relative fields inside collections; a `\* SpellOut` switch receives the original JSON value at its position in the switch sequence.

A shared registry supports concurrent reads after configuration, not registration during merges. Its formatters must be thread-safe and have singleton-compatible dependencies. A formatter with scoped dependencies needs a consumer-provided scoped registry, engines resolved within that same scope, and appropriate scope disposal; never capture it in the default singleton registry. Provider validation does not inspect manually captured objects. Each merge needs independent inputs and options that are not mutated during use.

## Command-line application

The CLI uses the same DI registration as library consumers and is packaged as the `monoreport` .NET tool. To pack and install it locally into an isolated directory on macOS/Linux Bash or zsh:

```bash
dotnet pack MonoReport.Cli/MonoReport.Cli.csproj -c Release -o ./artifacts/packages
dotnet tool install --tool-path ./artifacts/tools MonoReport.Cli \
  --source ./artifacts/packages
./artifacts/tools/monoreport --help
```

On Windows PowerShell:

```powershell
dotnet pack MonoReport.Cli/MonoReport.Cli.csproj -c Release -o .\artifacts\packages
dotnet tool install --tool-path .\artifacts\tools MonoReport.Cli `
  --source .\artifacts\packages
.\artifacts\tools\monoreport.exe --help
```

Remove `artifacts` when the isolated package and installation are no longer needed. The built DLL remains directly executable for development and troubleshooting.

1. `--template <path>` / `-t <path>` supplies the required DOCX template.
2. `--output <path>` / `-o <path>` writes a file after a successful merge. Omit it or use `-` for binary stdout. Existing files are overwritten only after rendering succeeds.
3. Choose one JSON source: `--json '<JSON>'`, `--json-file <path>`, or `--stdin`. File input is UTF-8; quote inline JSON for your shell. With no source option, the CLI reads stdin until EOF.
4. `--culture <culture>` selects numeric/date culture, such as `en-GB`; the default is invariant.
5. `--help` / `-h` shows help.
6. Exit codes are `0` for success, `1` for input/render/output errors, and `2` for invalid arguments. Diagnostics go to stderr; generation stdout contains only DOCX bytes.

### CLI template validation

`--validate --template <path>` (or `-t <path>`) directly invokes the same injected Core validator as the HTTP endpoint. Only these options are accepted in validation mode; JSON sources, stdin, culture, output, generation options, positional inputs and duplicates are argument errors. `--help` retains precedence. Validation never reads stdin, performs a merge, or writes DOCX bytes.

On macOS/Linux Bash or zsh, and on Windows after the tool directory is on `PATH`:

```bash
monoreport --validate --template Samples/sample.template.docx
```

To validate an invalid template, replace the template path with the document to inspect. Exit `0` means valid; exit `1` means invalid template or operational execution failure; exit `2` means invalid arguments. Completed validation writes exactly one JSON result to stdout with the same structure, issue codes/messages/context, nulls, ordering and warnings as HTTP validation. Invalid/corrupt/readable empty local files produce invalid JSON and exit 1 without operational stderr; empty HTTP uploads instead return 400. Missing/unreadable files and unexpected execution/output failures produce safe operational diagnostics on stderr and exit 1. Failures before writing leave stdout empty; an output stream failure can interrupt JSON. Caller-owned stdin/stdout stay open. Generation behavior remains unchanged when `--validate` is absent.

### Construction estimate sample

`Samples` contains `sample.template.docx`, `sample.json`, and `sample.docx`. The estimate identifies the Riverside Community Centre in Moscow and Northwind Construction LLC, repeats three material lines, and totals RUB 125,430.50. Labels and source data are English; the generated RUB amount in words is Russian. Quantities, prices, line totals, and summaries are supplied in JSON; MonoReport does not calculate business values.

Run commands from the repository root. This single-line build command works in macOS/Linux Bash or zsh, Windows PowerShell, and Windows CMD:

```bash
dotnet build MonoReport.Cli/MonoReport.Cli.csproj -c Release
```

Generate from a JSON file on macOS/Linux Bash or zsh:

```bash
monoreport --template Samples/sample.template.docx \
  --json-file Samples/sample.json \
  --output sample.docx \
  --culture en-GB
```

Pass inline JSON on macOS/Linux Bash or zsh:

```bash
monoreport --template Samples/sample.template.docx \
  --json "$(cat Samples/sample.json)" \
  --output sample.docx \
  --culture en-GB
```

Read stdin and redirect binary stdout on macOS/Linux Bash or zsh. These shells preserve binary stdout when redirecting it to a file:

```bash
monoreport --template Samples/sample.template.docx \
  --culture en-GB \
  < Samples/sample.json > sample.docx
```

On Windows PowerShell, read UTF-8 stdin and use `--output` so DOCX bytes are written directly by MonoReport:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Get-Content -Raw -Encoding UTF8 Samples/sample.json | `
  monoreport `
    --template Samples/sample.template.docx `
    --stdin `
    --output sample.docx `
    --culture en-GB
```

On Windows CMD, use caret (`^`) only for CMD line continuation. Read stdin and use `--output` instead of redirecting binary stdout:

```cmd
monoreport --template Samples/sample.template.docx ^
  --stdin ^
  --culture en-GB ^
  --output sample.docx ^
  < Samples\sample.json
```

Windows PowerShell 5.1 text redirection is unsuitable for binary DOCX; use `--output`. PowerShell versions do not all handle native binary stdout redirection identically, so `--output` is the portable Windows choice. With unredirected stdin, finish your JSON input with EOF so generation can proceed.

## Current limitations

1. DOCX generation and structural validation through Core, CLI, and HTTP are implemented. XLSX is planned; Word Add-in, conditions, and HTML/PDF output are not included.
2. Other Word fields, such as `PAGE`, are preserved but not evaluated. The engine does not execute template code or business calculations.
3. Manual Microsoft Word inspection remains useful for visual layout; automated checks validate Open XML and observable fields, formatting, and document structure.
