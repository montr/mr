using Microsoft.Extensions.DependencyInjection.Extensions;
using MonoReport.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the default MonoReport engine and shared, setup-time formatter registry.</summary>
public static class MonoReportServiceCollectionExtensions
{
    public static IServiceCollection AddMonoReport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<MergeEngine>();
        services.TryAddTransient<TemplateAnalyzer>();
        services.TryAddTransient<DocxStoryReader>();
        services.TryAddTransient<TemplateStructureValidator>();
        services.TryAddTransient<DocumentRenderer>();
        services.TryAddTransient<DataResolver>();
        services.TryAddTransient<OoxmlFieldParser>();
        services.TryAddTransient<FieldCodeParser>();
        services.TryAddTransient<RegionParser>();
        services.TryAddTransient<ValueFormatter>();
        services.TryAddTransient<CachedResultWriter>();
        services.TryAddSingleton<RubSpellOutFormatter>();
        services.TryAddSingleton(provider => new FormatterRegistry(new Dictionary<string, IValueFormatter>
        {
            [RubSpellOutFormatter.FormatterName] = provider.GetRequiredService<RubSpellOutFormatter>()
        }));

        return services;
    }
}
