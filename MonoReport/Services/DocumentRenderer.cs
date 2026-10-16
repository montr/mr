using System.Text.Json;
using System.Xml.Linq;
using MonoReport.Models;

namespace MonoReport.Services
{
    public sealed class DocumentRenderer(DataResolver resolver, OoxmlFieldParser fieldParser,
            RegionParser regionParser, ValueFormatter valueFormatter, CachedResultWriter resultWriter)
    {
        private readonly DataResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        private readonly OoxmlFieldParser _fieldParser = fieldParser ?? throw new ArgumentNullException(nameof(fieldParser));
        private readonly RegionParser _regionParser = regionParser ?? throw new ArgumentNullException(nameof(regionParser));
        private readonly ValueFormatter _valueFormatter = valueFormatter ?? throw new ArgumentNullException(nameof(valueFormatter));
        private readonly CachedResultWriter _resultWriter = resultWriter ?? throw new ArgumentNullException(nameof(resultWriter));

        public void Preflight(XElement root, string location)
        {
            _fieldParser.Validate(root, location);
            _regionParser.Validate(_fieldParser.Parse(root, location));
        }

        public void Render(XElement root, string location, MergeContext context)
        {
            var fields = _fieldParser.Parse(root, location);
            var regions = _regionParser.Parse(fields);
            Render(root, location, context, fields, regions);
            if (context.Errors.Count != 0)
                throw new TemplateException(context.FirstError!, context.Errors, context.LimitReached);
        }

        internal bool Render(XElement root, string location, MergeContext context,
            IReadOnlyList<FieldReference> fields, IReadOnlyList<Region> regions)
        {
            var regionsByStart = regions.ToDictionary(region => region.First);
            for (var i = 0; i < fields.Count; i++)
            {
                if (context.LimitReached) return false;
                if (regionsByStart.TryGetValue(i, out var region))
                {
                    if (!RenderRegion(location, context, region)) return false;
                    i = region.Last;
                    continue;
                }

                var field = fields[i];
                try
                {
                    var value = _resolver.Resolve(field.Field.Name, field.Field.Code, location, context);
                    _resultWriter.Write(field, _valueFormatter.Format(value, field.Field, context.Options, location));
                }
                catch (TemplateException error)
                {
                    _resultWriter.Write(field, string.Empty);
                    if (!context.Record(error, context.DataPath(field.Field.Name), field.ContextLocation)) return false;
                }
            }

            return true;
        }

        private bool RenderRegion(string location, MergeContext context, Region region)
        {
            var (first, last) = _regionParser.GetBlocks(region);
            var blocks = first.Parent!.Elements().SkipWhile(x => x != first).TakeWhile(x => x != last).Append(last).ToList();
            var collectionPath = context.DataPath(region.Start.Field.Collection);
            JsonElement collection;
            try
            {
                collection = _resolver.Resolve(region.Start.Field.Collection, region.Start.Field.Code, location, context);
                var emptyString = collection.ValueKind == JsonValueKind.String && collection.GetString() == string.Empty;
                if (collection.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined) && !emptyString)
                    throw new TemplateException($"Region '{region.Start.Field.Collection}': collection value must be an array ({location}).",
                        region.Start.Field.Collection, region.Start.Field.Code, location, code: "generation.invalid_data");
            }
            catch (TemplateException error)
            {
                foreach (var block in blocks) block.Remove();
                return context.Record(error, collectionPath, region.Start.ContextLocation);
            }

            if (collection.ValueKind == JsonValueKind.Array)
            {
                var itemIndex = 0;
                foreach (var item in collection.EnumerateArray())
                {
                    if (context.LimitReached) return false;
                    var copy = new XElement(first.Parent.Name, blocks.Select(x => new XElement(x)));
                    var copiedFields = _fieldParser.Parse(copy, location);
                    var start = copiedFields.First(x => x.Field.IsStart && x.Field.Collection == region.Start.Field.Collection);
                    var end = copiedFields.Last(x => x.Field.IsEnd && x.Field.Collection == region.End.Field.Collection);
                    var firstCopy = copy.Elements().First();
                    var lastCopy = copy.Elements().Last();
                    _resultWriter.Remove(start);
                    _resultWriter.Remove(end);
                    if (IsTechnicalBlock(firstCopy))
                    {
                        firstCopy.Remove();
                    }

                    if (lastCopy.Parent is not null && IsTechnicalBlock(lastCopy))
                    {
                        lastCopy.Remove();
                    }

                    context.Values.Push(item);
                    context.DataPaths.Push($"{collectionPath}[{itemIndex}]");

                    try
                    {
                        var innerFields = _fieldParser.Parse(copy, location);
                        var copiedRegions = _regionParser.Parse(innerFields);
                        if (!Render(copy, location + "/" + region.Start.Field.Collection, context,
                            innerFields, copiedRegions)) return false;
                    }
                    finally
                    {
                        context.DataPaths.Pop();
                        context.Values.Pop();
                    }

                    first.AddBeforeSelf(copy.Elements().ToList());
                    itemIndex++;
                }
            }

            foreach (var block in blocks) block.Remove();
            return true;
        }

        private static bool IsTechnicalBlock(XElement block)
        {
            return block.Descendants().Any(x => (x.Name == WordXml.TextElementName && string.IsNullOrWhiteSpace(x.Value) == false) || x.Name == WordXml.SimpleFieldElementName || x.Name == WordXml.FieldCharacterElementName || x.Name == WordXml.InstructionTextElementName || x.Name == WordXml.DrawingElementName || x.Name == WordXml.PictureElementName || x.Name == WordXml.TableElementName || x.Name == WordXml.BreakElementName || x.Name == WordXml.TabElementName) == false;
        }
    }
}
