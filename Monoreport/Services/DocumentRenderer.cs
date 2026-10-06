using System.Text.Json;
using System.Xml.Linq;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class DocumentRenderer(DataResolver resolver, OoxmlFieldParser fieldParser,
            RegionParser regionParser, ValueFormatter valueFormatter, CachedResultWriter resultWriter)
    {
        private readonly DataResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        private readonly OoxmlFieldParser _fieldParser = fieldParser ?? throw new ArgumentNullException(nameof(fieldParser));
        private readonly RegionParser _regionParser = regionParser ?? throw new ArgumentNullException(nameof(regionParser));
        private readonly ValueFormatter _valueFormatter = valueFormatter ?? throw new ArgumentNullException(nameof(valueFormatter));
        private readonly CachedResultWriter _resultWriter = resultWriter ?? throw new ArgumentNullException(nameof(resultWriter));

        public void Render(XElement root, string location, MergeContext context)
        {
            var fields = _fieldParser.Parse(root, location);
            var regions = _regionParser.Parse(fields);
            for (var i = 0; i < fields.Count; i++)
            {
                if (regions.Any(r => r.First <= i && i <= r.Last))
                {
                    continue;
                }

                var field = fields[i];
                var value = _resolver.Resolve(field.Field.Name, field.Field.Code, location, context);
                _resultWriter.Write(field, _valueFormatter.Format(value, field.Field, context.Options, location));
            }

            foreach (var region in regions)
            {
                var (first, last) = _regionParser.GetBlocks(region);
                var blocks = first.Parent!.Elements().SkipWhile(x => x != first).TakeWhile(x => x != last).Append(last).ToList();
                var collection = _resolver.Resolve(region.Start.Field.Collection, region.Start.Field.Code, location, context);
                if (collection.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    throw _regionParser.Error(region.Start, "collection value must be an array");
                }

                if (collection.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in collection.EnumerateArray())
                    {
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

                        try
                        {
                            Render(copy, location + "/" + region.Start.Field.Collection, context);
                        }
                        finally
                        {
                            context.Values.Pop();
                        }

                        first.AddBeforeSelf(copy.Elements().ToList());
                    }
                }

                foreach (var block in blocks)
                {
                    block.Remove();
                }
            }
        }

        private static bool IsTechnicalBlock(XElement block)
        {
            return block.Descendants().Any(x => (x.Name == WordXml.TextElementName && string.IsNullOrWhiteSpace(x.Value) == false) || x.Name == WordXml.SimpleFieldElementName || x.Name == WordXml.FieldCharacterElementName || x.Name == WordXml.InstructionTextElementName || x.Name == WordXml.DrawingElementName || x.Name == WordXml.PictureElementName || x.Name == WordXml.TableElementName || x.Name == WordXml.BreakElementName || x.Name == WordXml.TabElementName) == false;
        }
    }
}
