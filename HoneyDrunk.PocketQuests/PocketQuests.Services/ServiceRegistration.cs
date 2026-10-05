using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Catalogs;
using PocketQuests.Services.Exports;
using PocketQuests.Services.Lifecycle;
using PocketQuests.Services.Profiles;
using PocketQuests.Services.Progress;
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
        services.AddScoped<ProfileHistoryService>();
        services.AddScoped<QuestDefinitionService>();
        services.AddScoped<QuestSeriesService>();
        services.AddScoped<QuestOccurrenceService>();
        services.AddScoped<ProgressService>();
        services.AddScoped<QuestCommandHistoryService>();
        services.AddScoped(provider => new QuestService(
            provider.GetRequiredService<IAccountDataService>(),
            provider.GetRequiredService<ICurrentAccount>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ProfileHistoryService>(),
            provider.GetRequiredService<QuestDefinitionService>(),
            provider.GetRequiredService<QuestSeriesService>(),
            provider.GetRequiredService<QuestOccurrenceService>(),
            provider.GetRequiredService<ProgressService>(),
            provider.GetRequiredService<QuestCommandHistoryService>()));
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
