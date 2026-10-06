using System.Text;
using System.Xml.Linq;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class CachedResultWriter
    {
        private const string CarriageReturnLineFeed = "\r\n";
        private const string LineFeed = "\n";

        public void Write(FieldReference reference, string value)
        {
            if (reference.Simple is not null)
            {
                reference.Simple.SetAttributeValue(WordXml.DirtyAttributeName, WordXml.FalseValue);
            }
            else
            {
                reference.Begin!.SetAttributeValue(WordXml.DirtyAttributeName, WordXml.FalseValue);
            }

            var content = reference.Range(false).Where(IsRunContent).ToList();
            var texts = content.Where(x => x.Name == WordXml.TextElementName).ToList();
            // Preserve every run and its complete rPr. Distribute the new value across the existing runs.
            // Control characters get real OOXML break/tab elements rather than literal text characters.
            foreach (var item in content.Where(x => x.Name != WordXml.TextElementName))
            {
                item.Remove();
            }

            if (texts.Count == 0)
            {
                var text = new XElement(WordXml.TextElementName);
                if (reference.Simple is not null)
                {
                    var run = reference.Simple.Descendants(WordXml.RunElementName).FirstOrDefault();
                    if (run is null)
                    {
                        run = new XElement(WordXml.RunElementName);
                        reference.Simple.Add(run);
                    }

                    run.Add(text);
                }
                else
                {
                    if (reference.Separator is null)
                    {
                        var separator = new XElement(WordXml.FieldCharacterElementName, new XAttribute(WordXml.FieldCharacterTypeAttributeName, WordXml.FieldSeparatorValue));
                        reference.End!.AddBeforeSelf(separator, text);
                    }
                    else
                    {
                        reference.Separator.AddAfterSelf(text);
                    }
                }

                texts.Add(text);
            }

            var offset = 0;
            for (var i = 0; i < texts.Count; i++)
            {
                var length = i == texts.Count - 1 ? value.Length - offset : Math.Min(texts[i].Value.Length, value.Length - offset);
                var part = value.Substring(offset, length);
                offset += length;
                var nodes = new List<XElement>();
                var buffer = new StringBuilder();
                foreach (var ch in part.Replace(CarriageReturnLineFeed, LineFeed).Replace('\r', '\n'))
                {
                    if (ch is '\n' or '\t')
                    {
                        nodes.Add(new(WordXml.TextElementName, new XAttribute(WordXml.SpaceAttributeName, WordXml.PreserveSpaceValue), buffer.ToString()));
                        buffer.Clear();
                        nodes.Add(new(ch == '\n' ? WordXml.BreakElementName : WordXml.TabElementName));
                    }
                    else
                    {
                        buffer.Append(ch);
                    }
                }

                nodes.Add(new(WordXml.TextElementName, new XAttribute(WordXml.SpaceAttributeName, WordXml.PreserveSpaceValue), buffer.ToString()));
                texts[i].ReplaceWith(nodes);
            }
        }

        public void Remove(FieldReference reference)
        {
            if (reference.Simple is not null)
            {
                reference.Simple.Remove();
            }
            else
            {
                foreach (var node in reference.Range(true).Where(IsRunContent).ToList())
                {
                    node.Remove();
                }
            }
        }

        private static bool IsRunContent(XElement element) => element.Parent?.Name == WordXml.RunElementName && element.Name != WordXml.RunPropertiesElementName;
    }
}
