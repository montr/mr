using System.Text.Json.Serialization;

namespace MonoReport.Models;

public sealed record TemplateField
{
    public TemplateField(string name, string type, IEnumerable<IEnumerable<TemplateFieldSwitch>> formats,
        IEnumerable<TemplateField>? fields = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Formats = Array.AsReadOnly((formats ?? throw new ArgumentNullException(nameof(formats))).Select(format =>
            (IReadOnlyList<TemplateFieldSwitch>)Array.AsReadOnly(
                (format ?? throw new ArgumentException("Format sequences cannot be null.", nameof(formats))).ToArray())).ToArray());
        Fields = fields is null ? null : Array.AsReadOnly(fields.ToArray());
    }

    public string Name { get; }
    public string Type { get; }
    public IReadOnlyList<IReadOnlyList<TemplateFieldSwitch>> Formats { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TemplateField>? Fields { get; }
}
