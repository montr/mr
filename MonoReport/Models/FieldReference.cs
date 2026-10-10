using System.Xml.Linq;
using MonoReport.Services;

namespace MonoReport.Models
{
    public sealed record FieldReference(MergeField Field, XElement? Simple, XElement? Begin, XElement? Separator, XElement? End, XElement Root, string Location)
    {
        public XElement Anchor => Simple ?? Begin!;

        public string ContextLocation
        {
            get
            {
                var paragraph = Anchor.Ancestors(WordXml.ParagraphElementName).FirstOrDefault();
                var row = Anchor.Ancestors(WordXml.TableRowElementName).FirstOrDefault();
                var table = row?.Ancestors(WordXml.TableElementName).FirstOrDefault();
                var context = Location;
                if (paragraph is not null) context += $", paragraph {Root.Descendants(WordXml.ParagraphElementName).TakeWhile(x => x != paragraph).Count() + 1}";
                if (table is not null && row is not null)
                    context += $", table {Root.Descendants(WordXml.TableElementName).TakeWhile(x => x != table).Count() + 1}, row {table.Elements(WordXml.TableRowElementName).TakeWhile(x => x != row).Count() + 1}";
                var occurrence = Root.Descendants().TakeWhile(x => x != Anchor).Count() + 1;
                return context + $", field occurrence {occurrence}";
            }
        }

        public IEnumerable<XElement> Range(bool includeCode)
        {
            if (Simple is not null)
            {
                return Simple.Descendants();
            }

            var all = Root.Descendants().ToList();
            var start = all.IndexOf(includeCode ? Begin! : Separator ?? End!);
            var end = all.IndexOf(End!);
            return all.Skip(start + (includeCode ? 0 : 1)).Take(end - start + (includeCode ? 1 : -1));
        }
    }
}
