using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace MonoReport.Models;

internal sealed record AnalyzedStory(OpenXmlPart Part, XDocument Xml,
    IReadOnlyList<FieldReference> Fields, IReadOnlyList<Region> Regions, bool HierarchyIsSafe);
