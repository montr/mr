using DocumentFormat.OpenXml.Packaging;
using MonoReport.Models;

namespace MonoReport.Services;

public sealed class TemplateAnalyzer(DocxStoryReader storyReader, TemplateStructureValidator structureValidator)
{
    private readonly DocxStoryReader _storyReader = storyReader ?? throw new ArgumentNullException(nameof(storyReader));
    private readonly TemplateStructureValidator _structureValidator = structureValidator ?? throw new ArgumentNullException(nameof(structureValidator));

    public TemplateAnalysisResult Analyze(Stream template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!template.CanRead) throw new ArgumentException("Template stream must be readable.", nameof(template));
        using var buffer = new MemoryStream();
        template.CopyTo(buffer);
        buffer.Position = 0;
        var document = _storyReader.OpenForValidation(buffer, false, out var packageDiagnostic);
        if (packageDiagnostic is not null)
            return new([_structureValidator.Issue(packageDiagnostic)]);
        using (document)
        {
            var inspection = Inspect(document!);
            return new(inspection.Diagnostics.Select(diagnostic =>
                _structureValidator.Issue(diagnostic, DiagnosticPart(diagnostic.Location))), inspection.Warnings, inspection.Fields);
        }
    }

    internal TemplateInspection Inspect(WordprocessingDocument document)
    {
        var storyInspection = _storyReader.Inspect(document);
        var analyzedStories = new List<AnalyzedStory>();
        var diagnostics = storyInspection.Diagnostics.ToList();
        foreach (var story in storyInspection.Stories)
        {
            var part = story.Part.Uri.ToString();
            var analysis = _structureValidator.Analyze(story.Xml.Root!, part);
            diagnostics.AddRange(analysis.Diagnostics);
            analyzedStories.Add(new(story.Part, story.Xml, analysis.Fields, analysis.Regions, analysis.HierarchyIsSafe));
        }

        diagnostics = diagnostics.Select((diagnostic, index) => (diagnostic, index))
            .OrderBy(item => PartOrder(item.diagnostic.Location))
            .ThenBy(item => item.index)
            .Select(item => item.diagnostic)
            .ToList();

        var builder = new FieldTreeBuilder();
        foreach (var story in analyzedStories.Where(story => story.HierarchyIsSafe))
            builder.Add(story.Fields);
        return new(analyzedStories, diagnostics, builder.Warnings, builder.Fields);
    }

    private static (int Kind, string Part) PartOrder(string? location)
    {
        var part = location?.Split(',', 2)[0];
        return part switch
        {
            "/word/document.xml" => (0, part),
            not null when part.StartsWith("/word/header", StringComparison.Ordinal) => (1, part),
            not null when part.StartsWith("/word/footer", StringComparison.Ordinal) => (2, part),
            "/word/footnotes.xml" => (3, part),
            "/word/endnotes.xml" => (4, part),
            _ => (5, part ?? string.Empty)
        };
    }

    private static string? DiagnosticPart(string? location) => location?.Split(',', 2)[0];

    private sealed class FieldTreeBuilder
    {
        private readonly Scope _root = new();
        private readonly List<TemplateValidationIssue> _warnings = [];

        internal IReadOnlyList<TemplateField> Fields => _root.Items.Select(ToPublic).ToArray();
        internal IReadOnlyList<TemplateValidationIssue> Warnings => _warnings;

        internal void Add(IReadOnlyList<FieldReference> fields) => AddScope(_root, fields, 0, fields.Count);

        private void AddScope(Scope scope, IReadOnlyList<FieldReference> fields, int first, int last)
        {
            for (var index = first; index < last; index++)
            {
                var reference = fields[index];
                if (reference.Field.IsEnd) continue;
                if (!reference.Field.IsStart)
                {
                    AddScalar(scope, reference);
                    continue;
                }

                var end = FindEnd(fields, index, last);
                if (end < 0) return;
                var node = AddArray(scope, reference);
                if (node is not null)
                    AddScope(node.Children!, fields, index + 1, end);
                index = end;
            }
        }

        private void AddScalar(Scope scope, FieldReference reference)
        {
            var format = reference.Field.Switches.Select(ToPublic)
                .Where(item => item is not null).Cast<TemplateFieldSwitch>().ToArray();
            if (!scope.ByName.TryGetValue(reference.Field.Name, out var existing))
            {
                scope.Add(new(reference.Field.Name, "string", [format], null));
                return;
            }
            if (existing.Type != "string")
            {
                WarnConflict(scope, reference);
                return;
            }
            if (!existing.Formats.Any(item => item.SequenceEqual(format)))
                existing.Formats.Add(format);
        }

        private Node? AddArray(Scope scope, FieldReference reference)
        {
            if (!scope.ByName.TryGetValue(reference.Field.Collection, out var existing))
            {
                var node = new Node(reference.Field.Collection, "array", [], new());
                scope.Add(node);
                return node;
            }
            if (existing.Type == "array") return existing;
            WarnConflict(scope, reference);
            return null;
        }

        private void WarnConflict(Scope scope, FieldReference reference)
        {
            if (!scope.KindConflicts.Add(reference.Field.IsRegion ? reference.Field.Collection : reference.Field.Name)) return;
            _warnings.Add(new("template.field_kind_conflict",
                "The same field name is used as both a scalar and a collection; the first occurrence is represented.",
                reference.Field.IsRegion ? reference.Field.Collection : reference.Field.Name,
                reference.Location, reference.ContextLocation));
        }

        private static int FindEnd(IReadOnlyList<FieldReference> fields, int start, int last)
        {
            var depth = 0;
            for (var index = start + 1; index < last; index++)
            {
                if (fields[index].Field.IsStart) depth++;
                else if (fields[index].Field.IsEnd && depth-- == 0) return index;
            }
            return -1;
        }

        private static TemplateFieldSwitch? ToPublic(FieldSwitch fieldSwitch)
        {
            if (fieldSwitch.Name == WordFieldCodes.NumberFormatSwitch)
                return new("number", fieldSwitch.Argument);
            if (fieldSwitch.Name == WordFieldCodes.DateFormatSwitch)
                return new("date", fieldSwitch.Argument);
            if (fieldSwitch.Name != WordFieldCodes.GeneralFormatSwitch || !SupportedGeneral(fieldSwitch.Argument))
                return null;
            return new("general", fieldSwitch.Argument);
        }

        private static bool SupportedGeneral(string value) => value.ToUpperInvariant() is
            WordFieldCodes.UpperFormat or WordFieldCodes.LowerFormat or WordFieldCodes.CapsFormat or
            WordFieldCodes.FirstCapFormat or WordFieldCodes.SpellOutFormat or WordFieldCodes.MergeFormat;

        private static TemplateField ToPublic(Node node) => new(node.Name, node.Type, node.Formats,
            node.Children is null ? null : node.Children.Items.Select(ToPublic));

        private sealed class Scope
        {
            internal List<Node> Items { get; } = [];
            internal Dictionary<string, Node> ByName { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> KindConflicts { get; } = new(StringComparer.Ordinal);
            internal void Add(Node node) { Items.Add(node); ByName.Add(node.Name, node); }
        }

        private sealed class Node(string name, string type, IEnumerable<IReadOnlyList<TemplateFieldSwitch>> formats,
            Scope? children)
        {
            internal string Name { get; } = name;
            internal string Type { get; } = type;
            internal List<IReadOnlyList<TemplateFieldSwitch>> Formats { get; } = [.. formats];
            internal Scope? Children { get; } = children;
        }
    }
}
