# DOCX Template Merge Engine

## 1. Цель

Реализовать на C# бесплатный/open-source движок генерации DOCX из Word-шаблона и JSON-данных.

Вход:

1. DOCX-шаблон;
2. JSON с данными.

Выход:

3. DOCX с подставленными значениями.

Основной принцип:

4. использовать стандартный Word `MERGEFIELD` как базовый механизм шаблонизации;
5. не требовать установки Word Add-in;
6. использовать Open XML SDK;
7. не использовать коммерческие библиотеки;
8. сохранять шаблонные поля в результирующем DOCX.

Word Add-in может быть реализован в будущем как удобный UI для создания полей, но движок не должен зависеть от него.

---

# 2. Технологии

9. C# / .NET;
10. `DocumentFormat.OpenXml` / Open XML SDK;
11. без Aspose.Words, Syncfusion и других коммерческих DOCX-библиотек.

Движок должен работать непосредственно с `.docx` через OOXML.

---

# 3. Архитектура

Разделить систему на логические уровни:

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

Не смешивать:

12. работу с OOXML;
13. поиск и разбор field codes;
14. разрешение JSON;
15. форматирование значений;
16. изменение DOCX.

Core engine должен быть библиотекой. HTTP API — отдельный слой.

---

# 4. Базовый MERGEFIELD

Поддержать стандартный Word field:

```text
{ MERGEFIELD CompanyName }
```

JSON:

```json
{
  "CompanyName": "ООО Ромашка"
}
```

Результат:

```text
ООО Ромашка
```

Также поддержать:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

Важно: после merge `MERGEFIELD` **не должен удаляться и превращаться в обычный текст**.

Field code должен сохраняться в документе, а cached result должен обновляться.

---

# 5. OOXML fields

Учитывать, что Word может представлять field несколькими XML-элементами.

Например:

```xml
<w:fldChar w:fldCharType="begin"/>
<w:instrText> MERGEFIELD CompanyName </w:instrText>
<w:fldChar w:fldCharType="separate"/>
...
<w:fldChar w:fldCharType="end"/>
```

Также возможны другие допустимые OOXML-представления.

Нельзя предполагать:

17. что весь field code находится в одном XML node;
18. что field code находится в одном `w:r`;
19. что результат находится в одном `w:r`.

Нужен отдельный parser, который преобразует OOXML field в логическое представление, например:

```csharp
MergeField
{
    Name = "CompanyName",
    Switches = ...
}
```

---

# 6. JSON path

Поддержать простые поля:

```text
CompanyName
```

и вложенные поля:

```text
Customer.Name
Customer.Address.City
Contract.Number
```

Например:

```json
{
  "Contract": {
    "Number": "C-0000279"
  },
  "Customer": {
    "Name": "ООО Ромашка"
  }
}
```

Поле:

```text
{ MERGEFIELD Customer.Name }
```

должно дать:

```text
ООО Ромашка
```

На первом этапе достаточно dot notation.

---

# 7. Стандартные MERGEFIELD switches

Поддержать основные стандартные switches Word.

## `\*`

Текстовое/общее форматирование:

```text
{ MERGEFIELD Name \* Upper }
{ MERGEFIELD Name \* Lower }
{ MERGEFIELD Name \* Caps }
{ MERGEFIELD Name \* FirstCap }
{ MERGEFIELD Name \* MERGEFORMAT }
```

`MERGEFORMAT` не является преобразованием значения.

Он относится к сохранению/использованию форматирования результата поля.

Также не следует путать его с `Upper`, `Lower` и т.п.

---

## `\#`

Числовое форматирование:

```text
{ MERGEFIELD Amount \# "#,##0.00" }
```

---

## `\@`

Форматирование даты:

```text
{ MERGEFIELD ContractDate \@ "dd.MM.yyyy" }
```

---

## Комбинация switches

Разные стандартные switches могут использоваться одновременно, например:

```text
{ MERGEFIELD Amount \# "#,##0.00" \* MERGEFORMAT }
```

Для суммы прописью использовать расширение движка:

```text
{ MERGEFIELD Totals.Amount \* SpellOut }
```

`SpellOut` задаётся непосредственно в коде поля и обрабатывается движком Monoreport через собственный formatter. Это расширение движка, а не встроенное форматирование Word. Не требуется задавать теги content control или параметры форматтера в CLI. Поле с тем же путём без этого переключателя выводится обычным способом. Код поля, включая `SpellOut`, сохраняется при merge.

---

# 8. Formatter architecture

Создать расширяемую систему форматтеров.

Концептуально:

```csharp
formatter.Format(value, formatterName, options)
```

Стандартные Word switches должны обрабатываться отдельно от наших собственных formatter'ов.

Архитектура должна позволять в будущем добавить:

```text
upper
lower
number
date
currency
spellOut
```

но не следует реализовывать большой набор собственных formatter'ов на первом этапе.

---

# 9. Сумма прописью

Предусмотреть собственный formatter:

```text
spellOut
```

Например:

```json
{
  "Amount": 125430.50
}
```

может быть преобразовано в:

```text
Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек
```

Поддержать:

20. RUB;
21. рубли/рубля/рублей;
22. копейка/копейки/копеек;
23. отрицательные значения.

Архитектуру сделать расширяемой для других валют.

Реализацию `spellOut` можно сделать отдельным этапом.

---

# 10. Сохранение cached result

При обработке:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

нужно:

24. найти field;
25. сохранить его field code;
26. получить значение;
27. применить форматирование;
28. заменить cached result;
29. сохранить field в DOCX.

Например:

```xml
<w:fldSimple w:instr=" MERGEFIELD CompanyName \* MERGEFORMAT ">
    <w:r>
        <w:rPr>
            <w:b/>
        </w:rPr>
        <w:t>ООО Ромашка</w:t>
    </w:r>
</w:fldSimple>
```

После следующего merge:

```text
АО Вектор
```

должен измениться только результат поля, а не его field code.

---

# 11. Сохранение форматирования

При замене результата необходимо сохранять `w:rPr`.

Например, если поле в шаблоне жирное:

```xml
<w:rPr>
    <w:b/>
</w:rPr>
```

то после merge новое значение также должно быть жирным.

Не терять:

30. bold;
31. italic;
32. underline;
33. font;
34. font size;
35. color;
36. highlighting;
37. language;
38. другие `w:rPr`.

Особенно важно покрыть это тестами.

---

# 12. Повторный merge

Должен поддерживаться сценарий:

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

После второго merge:

39. field code остаётся;
40. значение обновляется;
41. форматирование не теряется;
42. документ остаётся корректным DOCX.

---

# 13. Коллекции / Mail Merge Regions

Для коллекций использовать семантику, близкую к Word Mail Merge Regions.

Начало области:

```text
{ MERGEFIELD TableStart:Items }
```

Конец:

```text
{ MERGEFIELD TableEnd:Items }
```

Это специальные технические fields.

Они не являются обычными полями данных.

---

# 14. Таблица с коллекцией

JSON:

```json
{
  "Items": [
    {
      "Name": "Товар 1",
      "Quantity": 2,
      "Price": 100
    },
    {
      "Name": "Товар 2",
      "Quantity": 3,
      "Price": 200
    }
  ]
}
```

Шаблон концептуально:

```text
┌─────────────────────────────────────┐
│ Наименование │ Кол-во │ Цена       │
├─────────────────────────────────────┤
│ TableStart:Items                    │
├─────────────────────────────────────┤
│ Name          │ Quantity │ Price    │
├─────────────────────────────────────┤
│ TableEnd:Items                      │
└─────────────────────────────────────┘
```

Внутри region обычные поля разрешаются относительно текущего элемента:

```text
{ MERGEFIELD Name }
{ MERGEFIELD Quantity }
{ MERGEFIELD Price }
```

НЕ использовать:

```text
{ MERGEFIELD Items.Name }
{ MERGEFIELD Items.Quantity }
```

внутри region.

---

# 15. Семантика collection context

При:

```text
TableStart:Items
```

создаётся context:

```text
current = Items[i]
```

Поэтому:

```text
{ MERGEFIELD Name }
```

означает:

```text
Items[i].Name
```

а:

```text
{ MERGEFIELD Quantity }
```

означает:

```text
Items[i].Quantity
```

После:

```text
TableEnd:Items
```

текущий context возвращается к родительскому.

Для реализации использовать context stack.

---

# 16. Повторение строк таблицы

Для каждого элемента `Items` необходимо создать копию template region.

Например:

```text
TableStart:Items
    строка-шаблон
TableEnd:Items
```

для двух элементов превращается в:

```text
строка для Items[0]
строка для Items[1]
```

Технические `TableStart` / `TableEnd` fields в результат не попадают.

---

# 17. Пустая коллекция

Для:

```json
{
  "Items": []
}
```

не должно появиться ни одной строки данных.

Если заголовок таблицы находится вне region:

```text
Header
TableStart:Items
Data row
TableEnd:Items
```

header сохраняется.

Технические строки, содержащие только `TableStart` / `TableEnd`, в итоговый документ не попадают.

---

# 18. Вложенные коллекции

Архитектура должна позволять:

```json
{
  "Orders": [
    {
      "Number": "A-001",
      "Items": [
        {
          "Name": "Товар 1",
          "Quantity": 2
        },
        {
          "Name": "Товар 2",
          "Quantity": 3
        }
      ]
    }
  ]
}
```

Шаблон:

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

После `TableEnd:Items`:

```text
Orders[i]
```

После `TableEnd:Orders`:

```text
parent context
```

Полноценную реализацию вложенных regions можно делать после базовой версии, но архитектура с самого начала должна учитывать context stack.

---

# 19. Region parser

`TableStart:Items` и `TableEnd:Items` должны распознаваться parser'ом как специальные элементы:

```csharp
RegionStart
{
    Collection = "Items"
}
```

и:

```csharp
RegionEnd
{
    Collection = "Items"
}
```

Parser должен проверять корректность вложенности:

```text
TableStart:Items
TableEnd:Items
```

и обнаруживать ошибки:

```text
TableEnd:Items
```

без соответствующего start.

Также обнаруживать несовпадение:

```text
TableStart:Items
TableEnd:Orders
```

---

# 20. Conditions

После базовых коллекций предусмотреть поддержку условий.

Возможная будущая семантика:

```text
IF
```

или отдельные условные regions.

Нужно поддержать в будущем:

43. true/false;
44. отсутствие значения;
45. сравнения;
46. условные блоки.

Не выполнять произвольный код из шаблона.

Условия не должны превращаться в механизм бизнес-логики.

---

# 21. Бизнес-логика

JSON должен приходить в engine уже подготовленным.

Например, расчёт:

```text
VAT = Amount * 0.20
```

должен выполняться приложением до вызова merge engine.

Merge engine занимается:

```text
value
  ↓
formatting
  ↓
rendering
```

а не бизнес-вычислениями.

---

# 22. Ошибки

Предусмотреть диагностируемые ошибки.

Неизвестное поле:

```text
{ MERGEFIELD Customer.Unknown }
```

например:

```text
Unknown template field: Customer.Unknown
```

Для отсутствующего значения предусмотреть configurable behavior:

47. empty string;
48. warning;
49. error.

Для первой версии можно использовать `empty string`.

Неверный formatter должен сообщать:

50. имя поля;
51. formatter;
52. исходный field code.

Ошибки region должны сообщать:

53. имя collection;
54. тип ошибки;
55. место в документе, если возможно.

---

# 23. Core API

Сначала реализовать библиотеку, например:

```csharp
MergeResult Merge(
    Stream template,
    JsonDocument data);
```

или эквивалентный API.

Не начинать реализацию с HTTP microservice.

HTTP API сделать поверх core library отдельным этапом.

---

# 24. HTTP API — будущий этап

В будущем:

```http
POST /api/templates/render
```

принимает:

56. DOCX template;
57. JSON data.

Возвращает:

58. generated DOCX.

Конкретный transport/API contract пока не является задачей первого этапа.

---

# 25. Тесты

Обязательно создать automated tests.

Минимальный набор:

### Basic fields

59. простой `MERGEFIELD`;
60. вложенный JSON path;
61. отсутствующее поле.

### Formatting

62. `Upper`;
63. `Lower`;
64. `Caps`;
65. `FirstCap`;
66. `MERGEFORMAT`;
67. `\#`;
68. `\@`.

### OOXML

69. field code в нескольких XML nodes;
70. field code в нескольких runs;
71. result в нескольких runs;
72. сохранение bold;
73. сохранение italic;
74. сохранение font;
75. сохранение font size;
76. сохранение остальных `w:rPr`.

### Re-merge

77. merge(template, data1);
78. merge(result, data2).

После второго merge field должен остаться корректным.

### Collections

79. `TableStart:Items`;
80. `TableEnd:Items`;
81. коллекция из 0 элементов;
82. коллекция из 1 элемента;
83. коллекция из N элементов;
84. несколько полей внутри region;
85. formatting внутри region;
86. header вне region сохраняется;
87. технические start/end fields не попадают в результат.

### Nested collections

88. вложенный `Orders → Items`;
89. корректное восстановление parent context.

---

# 26. Этапы реализации

## Этап 1 — базовый engine

Реализовать:

90. Open XML reading/writing;
91. field parser;
92. `MERGEFIELD`;
93. JSON path;
94. cached result;
95. сохранение field code;
96. сохранение formatting;
97. `Upper`;
98. `Lower`;
99. `Caps`;
100. `FirstCap`;
101. `MERGEFORMAT`;
102. `\#`;
103. `\@`;
104. automated tests.

Не реализовывать:

105. collections;
106. conditions;
107. spellOut;
108. HTTP API.

---

## Этап 2 — collections

Реализовать:

109. `TableStart:Items`;
110. `TableEnd:Items`;
111. collection context;
112. повторение table rows;
113. пустые коллекции;
114. несколько полей внутри region;
115. тесты.

---

## Этап 3 — nested collections

Реализовать:

116. context stack;
117. nested regions;
118. nested collections;
119. соответствующие тесты.

---

## Этап 4 — advanced formatting

Реализовать:

120. formatter registry;
121. `spellOut`;
122. currency;
123. дополнительные formatter'ы.

Для суммы прописью использовать согласованный синтаксис `\* SpellOut`, обрабатываемый движком Monoreport. Другие расширения вводить только при явном согласовании.

---

## Этап 5 — conditions

Реализовать условные regions/blocks.

---

## Этап 6 — HTTP service

ASP.NET Core service поверх библиотеки.

---

## Этап 7 — Word Add-in

Отдельный проект.

Add-in должен позволять пользователю:

124. выбирать доступное поле;
125. вставлять `MERGEFIELD`;
126. выбирать форматирование;
127. создавать `TableStart` / `TableEnd`;
128. выбирать collection;
129. не работать вручную с field codes.

При этом Add-in не является обязательным для работы engine.

---

# 27. Важные ограничения

130. Не использовать коммерческие DOCX libraries.
131. Использовать Open XML SDK.
132. Не удалять `MERGEFIELD` после merge.
133. Не превращать fields в обычный текст.
134. Не предполагать, что field code находится в одном XML node.
135. Не терять `w:rPr`.
136. Не смешивать бизнес-логику и template engine.
137. Не требовать Word Add-in.
138. Использовать `\* SpellOut` как согласованное расширение движка; не добавлять другие нестандартные переключатели без явного согласования.
139. Core engine должен быть независим от HTTP API.
140. Архитектура должна позволять расширение formatter'ов.
141. Архитектура должна позволять nested collection regions.
142. Не реализовывать сразу HTML/PDF renderer — это потенциальное будущее расширение, но не часть текущей задачи.

---

# 28. Definition of Done — первый этап

Следующий сценарий должен полностью работать.

Пользователь создаёт в Word:

```text
{ MERGEFIELD CompanyName \* MERGEFORMAT }
```

JSON:

```json
{
  "CompanyName": "ООО Ромашка"
}
```

После merge получается корректный DOCX, в котором:

143. отображается `ООО Ромашка`;
144. исходное форматирование сохранено;
145. `MERGEFIELD` всё ещё существует;
146. `MERGEFORMAT` остаётся в field code;
147. документ открывается в Word без повреждений.

Повторный merge:

```json
{
  "CompanyName": "АО Вектор"
}
```

должен изменить результат на:

```text
АО Вектор
```

при этом:

148. field code сохраняется;
149. форматирование сохраняется;
150. DOCX остаётся валидным.

После успешного завершения первого этапа переходить к `TableStart:Items` / `TableEnd:Items`.
