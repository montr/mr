using System.Text;
using System.Xml.Linq;

namespace MonoReport.Models
{
    internal sealed class FieldFrame(XElement begin)
    {
        public XElement Begin { get; } = begin;

        public XElement? Separator { get; set; }

        public StringBuilder Code { get; } = new();
    }
}
