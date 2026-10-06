namespace Monoreport.Models
{
    public sealed record Region(FieldReference Start, FieldReference End, int First, int Last);
}
