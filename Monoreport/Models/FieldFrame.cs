using System.Text;
using System.Xml.Linq;

namespace Monoreport.Models
{
    internal sealed class FieldFrame(XElement begin)
    {
        public XElement Begin { get; } = begin;

        public XElement? Separator { get; set; }

        public StringBuilder Code { get; } = new();
    }
}
