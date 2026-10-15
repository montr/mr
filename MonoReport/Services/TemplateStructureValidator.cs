using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Services;

public sealed class TemplateStructureValidator(OoxmlFieldParser fieldParser, RegionParser regionParser)
{
    private readonly OoxmlFieldParser _fieldParser = fieldParser ?? throw new ArgumentNullException(nameof(fieldParser));
    private readonly RegionParser _regionParser = regionParser ?? throw new ArgumentNullException(nameof(regionParser));

    public IReadOnlyList<TemplateValidationIssue> Validate(XElement root, string part)
    {
        return Inspect(root, part).Select(diagnostic => Issue(diagnostic, part)).ToArray();
    }

    internal IReadOnlyList<StructuralDiagnostic> Inspect(XElement root, string part)
        => Analyze(root, part).Diagnostics;

    internal StoryStructureAnalysis Analyze(XElement root, string part)
    {
        var inspection = _fieldParser.Inspect(root, part);
        var errors = inspection.Diagnostics.ToList();
        IReadOnlyList<Region> regions = [];
        var hierarchyIsSafe = inspection.StructureIsSafe;
        if (inspection.StructureIsSafe)
        {
            var regionErrors = _regionParser.Inspect(inspection.Fields);
            errors.AddRange(regionErrors);
            hierarchyIsSafe = regionErrors.Count == 0 && inspection.Diagnostics.All(diagnostic =>
                diagnostic.Code != "template.invalid_field_syntax" || !LooksLikeRegion(diagnostic.Field));
            if (regionErrors.Count == 0)
                regions = _regionParser.Parse(inspection.Fields);
        }
        return new(inspection.Fields, regions, errors, hierarchyIsSafe);
    }

    internal TemplateValidationIssue Issue(StructuralDiagnostic error, string? part = null) =>
        new(error.Code, SafeMessage(error.Code), error.Field, part, error.Location);

    private static string SafeMessage(string code) => code switch
    {
        "template.invalid_package" => "The content is not a readable DOCX package.",
        "template.unsupported_structure" => "The DOCX document structure is unsupported.",
        "template.invalid_xml" => "A document part contains invalid XML.",
        "template.invalid_field_syntax" => "A MERGEFIELD instruction has invalid syntax.",
        "template.invalid_field_structure" => "The Word field structure is invalid.",
        "template.invalid_region" => "A collection region is invalid.",
        "template.overlapping_regions" => "Collection regions overlap or share unsupported boundaries.",
        "template.unsupported_nested_field" => "Nested Word fields inside MERGEFIELD results are unsupported.",
        _ => "The template is invalid."
    };

    private static bool LooksLikeRegion(string? field) => field is not null &&
        (field.StartsWith(WordFieldCodes.RegionStartPrefix, StringComparison.OrdinalIgnoreCase) ||
         field.StartsWith(WordFieldCodes.RegionEndPrefix, StringComparison.OrdinalIgnoreCase));
}
