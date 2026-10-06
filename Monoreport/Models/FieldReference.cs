using System.Xml.Linq;

namespace Monoreport.Models
{
    public sealed record FieldReference(MergeField Field, XElement? Simple, XElement? Begin, XElement? Separator, XElement? End, XElement Root, string Location)
    {
        public XElement Anchor => Simple ?? Begin!;

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
