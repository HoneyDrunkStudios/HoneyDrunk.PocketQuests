using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Services;

namespace PocketQuests.Api.Quests;

/// <summary>Registers the canonical relational product and lifecycle persistence boundary.</summary>
public static class QuestPersistence
{
    /// <summary>Registers scoped EF data services, domain workflows and application adapters.</summary>
    /// <param name="builder">Product host.</param>
    public static void AddQuestPersistence(this WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("quests") ?? throw new InvalidOperationException("SQL Server connection 'quests' is required.");
        builder.Services.AddQuestDataServices(connection);
        builder.Services.AddQuestBusinessServices();
        builder.Services.AddScoped<QuestStore>();
        builder.Services.AddScoped<IQuestStore>(services => services.GetRequiredService<QuestStore>());
        builder.Services.AddScoped<ISyncAnchors>(services => services.GetRequiredService<QuestStore>());
        builder.Services.AddHostedService<LifecycleMaintenance>();
        if (builder.Configuration.GetValue("Persistence:ReconciliationEnabled", true))
            builder.Services.AddHostedService<ReconciliationMaintenance>();
    }
}
