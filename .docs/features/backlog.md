# Product Backlog

Potential work for MonoReport. Items are not approved Features; scope and priorities require Product Owner approval.

## Product Direction

1. **XLSX support** — Excel templates and generation; preserve formulas, styles, and workbook structure. Reuse DOCX components only where appropriate.
2. **Conditional rendering** — conditions in templates without arbitrary code execution or business calculations.
3. **Word Add-in** — visual template authoring with field and collection insertion. Define how data sources and server connections work first.
4. **Extended formatting** — additional field formats and currency support.
5. **MCP integration** — revisit when concrete consumers and use cases are identified.

## Engineering Backlog

1. **Release automation** — GitHub Releases and automatic release notes from commits.
2. **UI polish** — fix remaining minor visual issues, after confirming they still exist.
3. **Ant Design review** — consistent and appropriate use of components.
4. **Dependency audit** — review current npm audit findings.
5. **C# formatting and code style** — establish and automate consistent rules.
6. **UI performance** — measure bundle size and loading before optimizing.
7. **API documentation** — verify OpenAPI and interactive documentation coverage; address confirmed gaps only.
