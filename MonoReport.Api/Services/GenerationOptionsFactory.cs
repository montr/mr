using System.Globalization;
using MonoReport.Models;

namespace MonoReport.Api.Services;

public class GenerationOptionsFactory
{
    public virtual MergeOptions Create(CultureInfo culture) => new() { Culture = culture };
}
