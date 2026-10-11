# MonoReport

MonoReport generates DOCX documents from Microsoft Word templates and JSON data. It preserves Word fields and formatting, repeats collection regions, and supports custom value formatters. It requires .NET SDK 10; Microsoft Word is needed to edit or inspect templates, not to run the engine.

1. `MonoReport` is the library.
2. `MonoReport.Cli` is the command-line application.
3. `MonoReport.Tests` contains the automated tests, including Open XML validation.

Build and test from the repository root:

```bash
dotnet build MonoReport.slnx -c Release
dotnet test MonoReport.slnx -c Release
```

![Word template, JSON data, and generated construction estimate](.docs/Template%20to%20JSON%20to%20Document%20Workflow.png)

This illustration shows the workflow conceptually. For working templates, use the actual Word MERGEFIELD syntax and item-relative paths described in [Collections](#collections); the pictured field notation and JSON are illustrative.

## API

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

The CLI uses the same DI registration as library consumers. Invoke its built DLL to keep build messages out of binary output.

1. `--template <path>` / `-t <path>` supplies the required DOCX template.
2. `--output <path>` / `-o <path>` writes a file after a successful merge. Omit it or use `-` for binary stdout. Existing files are overwritten only after rendering succeeds.
3. Choose one JSON source: `--json '<JSON>'`, `--json-file <path>`, or `--stdin`. File input is UTF-8; quote inline JSON for your shell. With no source option, the CLI reads stdin until EOF.
4. `--culture <culture>` selects numeric/date culture, such as `en-GB`; the default is invariant.
5. `--help` / `-h` shows help.
6. Exit codes are `0` for success, `1` for input/render/output errors, and `2` for invalid arguments. Diagnostics go to stderr; generation stdout contains only DOCX bytes.

### Construction estimate sample

`Samples` contains `sample.template.docx`, `sample.json`, and `sample.docx`. The estimate identifies the Riverside Community Centre in Moscow and Northwind Construction LLC, repeats three material lines, and totals RUB 125,430.50. Labels and source data are English; the generated RUB amount in words is Russian. Quantities, prices, line totals, and summaries are supplied in JSON; MonoReport does not calculate business values.

Run commands from the repository root. Build once:

```bash
dotnet build MonoReport.Cli/MonoReport.Cli.csproj -c Release
```

Generate from a JSON file in Bash:

```bash
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll \
  --template Samples/sample.template.docx \
  --json-file Samples/sample.json \
  --output sample.docx \
  --culture en-GB
```

Pass inline JSON in Bash:

```bash
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll \
  --template Samples/sample.template.docx \
  --json "$(cat Samples/sample.json)" \
  --output sample.docx \
  --culture en-GB
```

Read stdin and redirect binary stdout in Bash:

```bash
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll \
  --template Samples/sample.template.docx \
  --culture en-GB \
  < Samples/sample.json > sample.docx
```

Read UTF-8 stdin and write a file in PowerShell:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Get-Content -Raw -Encoding UTF8 Samples/sample.json |
  dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll `
    --template Samples/sample.template.docx `
    --stdin `
    --output sample.docx `
    --culture en-GB
```

Read stdin and redirect binary stdout in `cmd.exe`:

```cmd
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll ^
  --template Samples/sample.template.docx ^
  --culture en-GB ^
  < Samples/sample.json > sample.docx
```

Windows PowerShell 5.1 text redirection is unsuitable for binary DOCX; use `--output`. With unredirected stdin, finish your JSON input with EOF so generation can proceed.

## Current limitations

1. DOCX is implemented. XLSX is planned; HTTP API, Word Add-in, conditions, and HTML/PDF output are not included.
2. Other Word fields, such as `PAGE`, are preserved but not evaluated. The engine does not execute template code or business calculations.
3. Manual Microsoft Word inspection remains useful for visual layout; automated checks validate Open XML and observable fields, formatting, and document structure.
