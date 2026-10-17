using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Data.DataServices;

namespace PocketQuests.Api.Quests;

/// <summary>Registers the canonical relational product and lifecycle persistence boundary.</summary>
public static class QuestPersistence
{
    /// <summary>Registers scoped EF data services and private maintenance workers.</summary>
    /// <param name="builder">Product host.</param>
    public static void AddQuestPersistence(this WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("quests") ?? throw new InvalidOperationException("SQL Server connection 'quests' is required.");
        builder.Services.AddQuestDataServices(connection);
        builder.Services.AddHostedService<LifecycleMaintenance>();
        if (builder.Configuration.GetValue("Persistence:ReconciliationEnabled", true))
            builder.Services.AddHostedService<ReconciliationMaintenance>();
    }
}
