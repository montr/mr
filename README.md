# Monoreport — DOCX Template Merge Engine

Проекты на **.NET 10 / C# 14**:

1. `Monoreport` — движок на Open XML SDK 3.5.1; пространства имён `Monoreport.Services` и `Monoreport.Models`.
2. `Monoreport.Tests` — автоматические тесты xUnit, включая проверку результатов через `OpenXmlValidator`.
3. `Monoreport.Cli` — консольное приложение для генерации DOCX из шаблона и JSON.

Требуется .NET SDK 10. Установка Microsoft Word не нужна.

```powershell
dotnet build Monoreport.slnx -c Release
dotnet test Monoreport.slnx -c Release
```

`NuGet.Config` использует только официальный HTTPS-источник nuget.org.

## API

```csharp
using System.Text.Json;
using Monoreport.Services;
using Monoreport.Models;

using var template = File.OpenRead("template.docx");
using var data = JsonDocument.Parse("""
    {"CompanyName":"ООО Ромашка","Amount":125430.50}
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

В Word вставьте настоящее поле, например через **Вставка → Экспресс-блоки → Поле → MergeField**. Набранный вручную текст `{ MERGEFIELD ... }` не является Word-полем.

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
{ MERGEFIELD Customer.Address.City }
{ MERGEFIELD Amount \# "#,##0.00" }
{ MERGEFIELD ContractDate \@ "dd.MM.yyyy" }
```

Входной поток читается от текущей позиции, не изменяется и не закрывается. `JsonDocument` также остаётся во владении вызывающего кода. Результат — самостоятельный массив байтов DOCX. Обрабатываются основной документ, колонтитулы, обычные и концевые сноски.

## Поля и форматирование

Поддерживаются `w:fldSimple` и сложные поля `begin / instrText / separate / end`, в том числе инструкции и результаты в нескольких узлах, runs и абзацах. При отсутствии cached result он создаётся. Код поля сохраняется; атрибут `dirty` сбрасывается, чтобы не помечать заполненное поле как требующее обновления.

Сохраняются все существующие `w:rPr`. Если результат занимал несколько runs, новый текст распределяется по их прежним длинам, остаток помещается в последний run. Это сохраняет оформление каждого фрагмента. Переводы строк и табуляция становятся `w:br` и `w:tab`.

Стандартные switches:

3. `\* Upper`, `Lower`, `Caps`, `FirstCap`, `MERGEFORMAT`;
4. `\#` — числовые маски, включая `#,##0.00`;
5. `\@` — даты, включая `dd.MM.yyyy`;
6. комбинации switches в порядке, указанном в поле.

Числовые и календарные маски обрабатываются средствами .NET. Полная грамматика всех исторических Word picture switches не реализована. Даты в JSON рекомендуется передавать в ISO 8601. Культура по умолчанию — `InvariantCulture`, её можно задать явно:

```csharp
var options = new MergeOptions
{
    Culture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU"),
    MissingValues = MissingValueBehavior.Warning
};
var result = engine.Merge(template, data, options);
foreach (var warning in result.Warnings)
    Console.WriteLine(warning.Message);
```

Отсутствующие поля по умолчанию заменяются пустой строкой. Также доступны предупреждение и ошибка. JSON `null` означает пустое значение. Имена JSON-свойств регистрозависимы; пути используют dot notation. Объекты и массивы нельзя подставлять в скалярные поля. Ошибки шаблона представлены `TemplateException` с полем, исходным кодом, форматтером и URI части документа, когда эти сведения доступны.

Для повторного merge обычных полей передайте `result.Document` через новый `MemoryStream`: значения обновятся, коды и оформление останутся.

## Коллекции

```text
Заголовок таблицы                    ← вне области, сохраняется
{ MERGEFIELD TableStart:Items }      ← отдельная техническая строка
{ MERGEFIELD Name } | { MERGEFIELD Quantity } | { MERGEFIELD Price \# "0.00" }
{ MERGEFIELD TableEnd:Items }        ← отдельная техническая строка
```

```json
{"Items":[{"Name":"Товар 1","Quantity":2,"Price":100},{"Name":"Товар 2","Quantity":3,"Price":200}]}
```

Для каждого элемента копируется область. Пустой массив не создаёт строк данных; заголовок сохраняется. Технические поля удаляются, строки/абзацы только с этими полями также удаляются. Обычные `MERGEFIELD` в строках остаются полями.

Границы области должны находиться в строках одной таблицы либо в абзацах одного родителя. Оба маркера также могут находиться в одной строке данных, окружая её поля. Произвольные диапазоны между разными таблицами и частями документа не поддерживаются. Для вложенных областей используйте отдельные строки или абзацы границ.

Внутри области пути разрешаются строго относительно текущего элемента: `Name`, а не `Items.Name`. Неявного поиска в родительском объекте нет. Вложенные `Orders → Items` поддерживаются через стек контекстов; после завершения внутренней области восстанавливается контекст заказа, после внешней — корневой объект.

**Повторное разворачивание коллекций:** используйте исходный шаблон. После merge технические маркеры удалены по SPEC; результат не хранит исходную область и привязку каждой строки к индексу массива. Повторный merge готового документа поддерживается для обычных полей, но не восстанавливает структуру коллекций.

## Собственные форматтеры

Сумма прописью задаётся прямо в коде поля:

```text
{ MERGEFIELD Totals.Amount \* SpellOut }
{ MERGEFIELD Totals.Amount \* SpellOut \* MERGEFORMAT }
```

В Word вставьте MERGEFIELD, включите отображение кода поля сочетанием Shift+F9 и добавьте `\* SpellOut` после имени поля. Теги и дополнительные параметры CLI не нужны. `SpellOut` — расширение движка Monoreport, а не встроенный форматтер Word: его результат вычисляется при запуске Monoreport. Регистр имени не важен. Поле с тем же JSON-путём без `SpellOut` продолжает выводиться числом; код поля и оформление сохраняются после merge.

Для программной настройки форматтеров также доступна привязка через API:

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

`spellOut` поддерживает RUB, склонения, отрицательные числа и округление до копеек от нуля. Диапазон после округления: модуль суммы меньше 10¹⁸ рублей. Для других валют и преобразований реализуйте `IValueFormatter` и зарегистрируйте его через `FormatterRegistry.Register`. Привязка задаётся по имени поля, включая поля внутри коллекций. Форматтер, назначенный через API, выполняется перед переключателями поля; `\* SpellOut` выполняется на своём месте в последовательности переключателей и получает исходное JSON-значение.

## Архитектура и границы текущей реализации

7. `OoxmlFieldParser` / `FieldCodeParser` — OOXML-поля и разбор инструкций.
8. `MergeField`, `FieldReference`, `Region` / `RegionParser` — модель и проверка вложенности.
9. `DataResolver` — JSON-пути и стек контекстов.
10. `ValueFormatter`, `FormatterRegistry`, `RubSpellOutFormatter` — форматирование.
11. `CachedResultWriter`, `DocumentRenderer`, `MergeEngine` — обновление результатов, повторение областей и работа с DOCX-пакетом.

Реализованы базовый этап SPEC, обычные и вложенные коллекции, расширяемый реестр и RUB spellOut. Условия, HTTP service, Word Add-in и HTML/PDF относятся к будущим этапам SPEC и не включены. Бизнес-вычисления и выполнение кода из шаблона отсутствуют. Остальные Word-поля, например `PAGE`, движок не вычисляет.

Тесты проверяют поля, все перечисленные switches, разорванные инструкции, сохранение оформления, повторный merge, коллекции размера 0/1/N, вложенные контексты, колонтитулы, диагностику и расширение форматтеров. Успешные результаты проверяются Open XML Validator; визуальная проверка в Microsoft Word автоматически не выполняется.

Сервисы парсинга, форматирования и записи работают через экземпляры, а зависимости передаются в конструкторы. Граф сервисов собирается вызывающим приложением и передаётся в публичные конструкторы. Конструктора MergeEngine без зависимостей нет. Реестр получает форматтеры снаружи. Контекст данных и предупреждения создаются отдельно для каждого вызова Merge.

## Monoreport.Cli — консольное приложение

Приложение ссылается на проект `Monoreport`. Вызов принимает путь к DOCX-шаблону, источник JSON и необязательный путь результата. Сервисы движка собираются в точке входа CLI, зависимости передаются через конструкторы.

1. `--template <путь>` / `-t <путь>` — обязательный путь к шаблону.
2. `--output <путь>` / `-o <путь>` — путь результата; существующий файл перезаписывается после успешного merge. Если параметр отсутствует или равен `-`, DOCX записывается в stdout как бинарные данные.
3. `--json '<JSON>'` — JSON непосредственно в аргументе командной строки. Экранирование кавычек зависит от используемой оболочки.
4. `--json-file <путь>` — путь к JSON-файлу в UTF-8.
5. `--stdin` — чтение UTF-8 JSON из стандартного ввода до EOF. Используется также по умолчанию, если `--json` и `--json-file` не указаны. Источники JSON взаимоисключающие.
6. `--culture <культура>` — культура числового и календарного форматирования, например `ru-RU`. По умолчанию — invariant culture.
7. `--help` / `-h` — справка.
8. Коды завершения: `0` — успех, `1` — ошибка чтения, merge или записи, `2` — ошибка аргументов. Диагностика направляется в stderr; при генерации stdout содержит только байты DOCX.

### Образец договоров контрагента

Рядом с проектом, в папке `Monoreport.Cli`, находятся `sample.template.docx` и `sample.json`. Шапка содержит наименование контрагента, ИНН/VATIN, КПП, адрес, контакт и email. Таблица повторяется по коллекции `Contracts` и выводит номер, дату, наименование, сумму и валюту договора. После таблицы выводятся количество договоров, общая сумма и сумма прописью.

В образце три договора в RUB на общую сумму 125 430,50. `Totals.Count` и `Totals.Amount` уже рассчитаны в JSON: движок не выполняет бизнес-вычисления. В обоих итоговых полях используется `Totals.Amount`: числовое поле имеет маску `\# "#,##0.00"`, а поле суммы прописью имеет код `{ MERGEFIELD Totals.Amount \* SpellOut }`. Движок автоматически выводит «Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек» без дополнительных параметров CLI и дублирования значения в JSON. Суммы разных валют перед объединением нужно подготовить в приложении.

Команды выполняются из корня репозитория. Исходный шаблон называется `sample.template.docx`, результирующий файл — `sample.docx`:

```powershell
dotnet build Monoreport.Cli/Monoreport.Cli.csproj -c Release
dotnet Monoreport.Cli/bin/Release/net10.0/Monoreport.Cli.dll --template Monoreport.Cli/sample.template.docx --json-file Monoreport.Cli/sample.json --output Monoreport.Cli/sample.docx --culture ru-RU
```

Чтение JSON из stdin в PowerShell с записью результата в файл:

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Get-Content -Raw -Encoding UTF8 Monoreport.Cli/sample.json | dotnet Monoreport.Cli/bin/Release/net10.0/Monoreport.Cli.dll --template Monoreport.Cli/sample.template.docx --stdin --output Monoreport.Cli/sample.docx --culture ru-RU
```

Передача JSON аргументом в Bash:

```bash
dotnet Monoreport.Cli/bin/Release/net10.0/Monoreport.Cli.dll --template Monoreport.Cli/sample.template.docx --json "$(cat Monoreport.Cli/sample.json)" --output Monoreport.Cli/sample.docx --culture ru-RU
```

Чтение stdin и запись бинарного stdout через перенаправление в Bash или `cmd.exe`:

```text
dotnet Monoreport.Cli/bin/Release/net10.0/Monoreport.Cli.dll --template Monoreport.Cli/sample.template.docx --culture ru-RU < Monoreport.Cli/sample.json > Monoreport.Cli/sample.docx
```

Для stdout вызывайте уже собранный DLL: сообщения сборки `dotnet run` не должны попадать в DOCX. В Windows PowerShell 5.1 используйте `--output`, поскольку его текстовое перенаправление `>` не подходит для бинарного DOCX. При `--stdin` без перенаправления приложение ждёт JSON и завершения ввода (EOF).
