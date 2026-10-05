using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PocketQuests.Services.Catalogs;
using PocketQuests.Services.Exports;
using PocketQuests.Services.Lifecycle;
using PocketQuests.Services.Profiles;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Reconciliation;
using PocketQuests.Services.Schedules;
using PocketQuests.Services.Synchronization;

namespace PocketQuests.Services;

/// <summary>Registers approved migrated operations with scoped persistence.</summary>
public static class ServiceRegistration
{
    /// <summary>Registers scoped feature services and the host clock.</summary>
    /// <param name="services">Host service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddQuestServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<QuestService>();
        services.AddScoped<IQuestService>(provider => provider.GetRequiredService<QuestService>());
        services.AddScoped<ProfileService>();
        services.AddScoped<IProfileService>(provider => provider.GetRequiredService<ProfileService>());
        services.AddScoped<SynchronizationService>();
        services.AddScoped<ISynchronizationService>(provider => provider.GetRequiredService<SynchronizationService>());
        services.AddScoped<ExportService>();
        services.AddScoped<IExportService>(provider => provider.GetRequiredService<ExportService>());
        services.AddScoped<ReconciliationService>();
        services.AddScoped<IReconciliationService>(provider => provider.GetRequiredService<ReconciliationService>());
        services.AddScoped<OccurrenceReadService>();
        services.AddScoped<LifecycleService>();
        services.AddScoped<ILifecycleService>(provider => provider.GetRequiredService<LifecycleService>());
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPlanningService, PlanningService>();
        return services;
    }
}
