# MonoReport — DOCX Template Merge Engine

Projects targeting **.NET 10 / C# 14**:

1. `MonoReport` — an engine built on Open XML SDK 3.5.1, with the `MonoReport.Services` and `MonoReport.Models` namespaces.
2. `MonoReport.Tests` — automated xUnit tests, including output validation with `OpenXmlValidator`.
3. `MonoReport.Cli` — a console application that generates DOCX documents from a template and JSON.

.NET SDK 10 is required. Microsoft Word does not need to be installed.

```powershell
dotnet build MonoReport.slnx -c Release
dotnet test MonoReport.slnx -c Release
```

`NuGet.Config` uses only the official HTTPS nuget.org source.

## API

```csharp
using System.Text.Json;
using MonoReport.Services;
using MonoReport.Models;

using var template = File.OpenRead("template.docx");
using var data = JsonDocument.Parse("""
    {"CompanyName":"Northwind Construction LLC","Amount":125430.50}
    """);

var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter>
{
    [RubSpellOutFormatter.FormatterName] = new RubSpellOutFormatter()
});
var fieldParser = new OoxmlFieldParser(new FieldCodeParser());
var renderer = new DocumentRenderer(new DataResolver(), fieldParser, new RegionParser(),
    new ValueFormatter(registry), new CachedResultWriter());
var engine = new MergeEngine(renderer);
MergeResult result = engine.Merge(template, data);
File.WriteAllBytes("result.docx", result.Document);
```

Insert an actual Word field, for example through **Insert → Quick Parts → Field → MergeField**. Manually typed text such as `{ MERGEFIELD ... }` is not a Word field.

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
{ MERGEFIELD Customer.Address.City }
{ MERGEFIELD Amount \# "#,##0.00" }
{ MERGEFIELD ContractDate \@ "dd.MM.yyyy" }
```

The input stream is read from its current position and is neither modified nor closed. The caller retains ownership of the `JsonDocument` as well. The result is an independent DOCX byte array. The engine processes the main document, headers, footers, footnotes, and endnotes.

## Fields and formatting

Both `w:fldSimple` and complex fields using `begin / instrText / separate / end` are supported, including instructions and results split across nodes, runs, and paragraphs. A missing cached result is created. Field codes are preserved; the `dirty` attribute is cleared so that populated fields are not marked as requiring an update.

All existing `w:rPr` properties are preserved. When a result spans multiple runs, the new text is distributed according to their original lengths, with any remainder placed in the last run. This preserves each fragment's formatting. Newlines and tabs become `w:br` and `w:tab` elements.

Standard switches:

1. `\* Upper`, `Lower`, `Caps`, `FirstCap`, `MERGEFORMAT`.
2. `\#` — numeric formats, including `#,##0.00`.
3. `\@` — date formats, including `dd.MM.yyyy`.
4. Combinations of switches in the order specified in the field.

Numeric and date formats use .NET formatting facilities. The complete grammar of all historical Word picture switches is not implemented. ISO 8601 is recommended for dates in JSON. The default culture is `InvariantCulture`; it can be set explicitly:

```csharp
var options = new MergeOptions
{
    Culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
    MissingValues = MissingValueBehavior.Warning
};
var result = engine.Merge(template, data, options);
foreach (var warning in result.Warnings)
    Console.WriteLine(warning.Message);
```

Missing fields are replaced with an empty string by default. Warning and error behaviors are also available. JSON `null` represents an empty value. JSON property names are case-sensitive, and paths use dot notation. Objects and arrays cannot be inserted into scalar fields. Template errors are reported as `TemplateException`, including the field, original code, formatter, and document-part URI when available.

To merge ordinary fields again, pass `result.Document` through a new `MemoryStream`: values are updated while field codes and formatting remain intact.

## Collections

```text
Table header                        ← outside the region; preserved
{ MERGEFIELD TableStart:Items }      ← separate marker row
{ MERGEFIELD Name } | { MERGEFIELD Quantity } | { MERGEFIELD Price \# "0.00" }
{ MERGEFIELD TableEnd:Items }        ← separate marker row
```

```json
{"Items":[{"Name":"Portland cement (25 kg bag)","Quantity":100,"Price":500},{"Name":"Concrete blocks (440 x 215 x 100 mm)","Quantity":250,"Price":261.722}]}
```

The region is copied for each item. An empty array produces no data rows, while the header remains. Marker fields are removed, as are rows or paragraphs containing only those fields. Ordinary `MERGEFIELD` fields in data rows remain fields.

Region boundaries must be in rows of the same table or in paragraphs sharing a parent. Both markers can also appear in a single data row, surrounding its fields. Arbitrary ranges across different tables or document parts are not supported. Use separate boundary rows or paragraphs for nested regions.

1. Independent regions must use separate paragraph or table-row ranges with no shared boundary block. Shared or overlapping block footprints, including containment through nested tables, are rejected; genuine logical parent/child regions remain supported.
2. Nested Word fields inside an ordinary or technical-marker `MERGEFIELD` cached result are unsupported. Move the nested field outside the outer result or replace that result with plain cached text. Instruction nesting and independent sibling Word fields retain their existing behavior.
3. The engine validates these structures in every processed story part before changing any story. A `TemplateException` identifies the field or conflicting collections, original part URI, paragraph/table-row context when available, and the required template correction. CLI rejection emits a diagnostic without producing DOCX output or replacing an existing output file.


Within a region, paths resolve strictly relative to the current item: use `Name`, rather than `Items.Name`. There is no implicit lookup in the parent object. Nested `Orders → Items` collections are supported through a context stack; finishing the inner region restores the order context, and finishing the outer region restores the root object.

**Expanding collections again:** use the original template. Marker fields are removed after a merge, as required by the specification; the result does not retain the original region or each row's array-index binding. Merging a generated document again supports ordinary fields but does not reconstruct collection structure.

## Custom formatters

An amount in words can be requested directly in the field code:

```text
{ MERGEFIELD Totals.Amount \* SpellOut }
{ MERGEFIELD Totals.Amount \* SpellOut \* MERGEFORMAT }
```

In Word, insert a MERGEFIELD, display its code with Shift+F9, and add `\* SpellOut` after the field name. No tags or additional CLI options are needed. `SpellOut` is a MonoReport extension rather than a built-in Word formatter: MonoReport computes its result when run. The formatter name is case-insensitive. A field using the same JSON path without `SpellOut` continues to display a number; field codes and formatting remain intact after a merge.

Formatters can also be bound programmatically through the API:

```csharp
var options = new MergeOptions
{
    FieldFormatters = new Dictionary<string, FormatterBinding>
    {
        ["Amount"] = new("spellOut", new Dictionary<string, string>
        {
            ["currency"] = "RUB"
        })
    }
};
var result = engine.Merge(template, data, options);
```

`125430.50` → `Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек`.

The built-in `spellOut` formatter produces Russian text for RUB amounts, regardless of the selected numeric/date culture. It supports grammatical inflection, negative numbers, and kopeck rounding away from zero. After rounding, the absolute amount must be less than 10¹⁸ rubles. For other currencies or transformations, implement `IValueFormatter` and register it with `FormatterRegistry.Register`. Bindings use field names, including fields inside collections. An API-bound formatter runs before field switches; `\* SpellOut` runs at its position in the switch sequence and receives the original JSON value.

## Architecture and current implementation boundaries

1. `OoxmlFieldParser` / `FieldCodeParser` — OOXML fields and instruction parsing.
2. `MergeField`, `FieldReference`, `Region` / `RegionParser` — models and nesting validation.
3. `DataResolver` — JSON paths and the context stack.
4. `ValueFormatter`, `FormatterRegistry`, `RubSpellOutFormatter` — formatting.
5. `CachedResultWriter`, `DocumentRenderer`, `MergeEngine` — cached-result updates, region repetition, and DOCX package handling.

The specification's basic stage, ordinary and nested collections, extensible registry, and RUB spell-out are implemented. Conditions, an HTTP service, a Word Add-in, and HTML/PDF belong to future specification stages and are not included. The engine does not perform business calculations or execute template code. Other Word fields, such as `PAGE`, are not evaluated by the engine.

Tests cover fields, all listed switches, split instructions, formatting preservation, repeated merges, collections of size 0/1/N, nested contexts, headers and footers, diagnostics, and formatter extension. Successful outputs are checked with Open XML Validator; visual inspection in Microsoft Word is not automated.

Parsing, formatting, and writing services are instances with constructor-injected dependencies. The consuming application composes the service graph and supplies it through public constructors. `MergeEngine` has no parameterless constructor. The registry receives formatters externally. Data context and warnings are created separately for each Merge call.

## MonoReport.Cli — console application

The application references the `MonoReport` project. It accepts a DOCX template path, a JSON source, and an optional output path. The CLI entry point composes engine services and supplies dependencies through constructors.

1. `--template <path>` / `-t <path>` — required template path.
2. `--output <path>` / `-o <path>` — output path; an existing file is overwritten after a successful merge. If omitted or set to `-`, DOCX is written to stdout as binary data.
3. `--json '<JSON>'` — JSON supplied directly as a command-line argument. Quote escaping depends on the shell.
4. `--json-file <path>` — path to a UTF-8 JSON file.
5. `--stdin` — read UTF-8 JSON from standard input until EOF. This is also the default when neither `--json` nor `--json-file` is specified. JSON sources are mutually exclusive.
6. `--culture <culture>` — numeric and date formatting culture, such as `en-GB`. The default is invariant culture.
7. `--help` / `-h` — help.
8. Exit codes: `0` — success; `1` — read, merge, or write error; `2` — argument error. Diagnostics go to stderr; during generation, stdout contains only DOCX bytes.

### Construction project cost estimate sample

The repository-root `Samples` directory contains `sample.template.docx`, `sample.json`, and the generated `sample.docx`. The header identifies the construction project and contractor, including tax ID, registration number, address, contact, and email. The table repeats the `Materials` collection and displays each material's reference, delivery date, description, quantity, unit, unit price, line total, and currency. The summary shows the number of material lines, total cost, and amount in words.

The example estimates materials for the Riverside Community Centre in Moscow, supplied by Northwind Construction LLC: Portland cement, concrete paving slabs, and washed building sand. The three lines total RUB 125,430.50. `LineTotal`, `Totals.Count`, and `Totals.Amount` are already calculated in JSON; the engine does not perform business calculations. Both total fields use `Totals.Amount`: the numeric field uses `\# "#,##0.00"`, while the amount-in-words field uses `{ MERGEFIELD Totals.Amount \* SpellOut }`. All sample labels and data are English except the engine-generated Russian RUB amount in words, retained to demonstrate the existing formatter. No additional CLI arguments or duplicate JSON value are required for that formatter. Convert amounts in different currencies in the consuming application before combining them.

Run these commands from the repository root. The source template is `sample.template.docx`, and the generated document is `sample.docx`:

```powershell
dotnet build MonoReport.Cli/MonoReport.Cli.csproj -c Release
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --json-file Samples/sample.json --output Samples/sample.docx --culture en-GB
```

Read JSON from stdin in PowerShell and write the result to a file:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Get-Content -Raw -Encoding UTF8 Samples/sample.json | dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --stdin --output Samples/sample.docx --culture en-GB
```

Pass JSON as a Bash argument:

```bash
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --json "$(cat Samples/sample.json)" --output Samples/sample.docx --culture en-GB
```

Read stdin and redirect binary stdout in Bash or `cmd.exe`:

```text
dotnet MonoReport.Cli/bin/Release/net10.0/MonoReport.Cli.dll --template Samples/sample.template.docx --culture en-GB < Samples/sample.json > Samples/sample.docx
```

For stdout, invoke the already-built DLL so that `dotnet run` build messages cannot enter the DOCX output. In Windows PowerShell 5.1, use `--output` because its text redirection with `>` is unsuitable for binary DOCX. With `--stdin` and no redirection, the application waits for JSON and the end of input (EOF).
