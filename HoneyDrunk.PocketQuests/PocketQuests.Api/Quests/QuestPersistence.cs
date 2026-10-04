using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.AccountLifecycle;
using PocketQuests.Data.Relational;
using PocketQuests.Data.Relational.Commands;
using PocketQuests.Data.Repositories;

namespace PocketQuests.Api.Quests;

/// <summary>Selects one coherent product/lifecycle persistence implementation through an explicit local cutover setting.</summary>
public static class QuestPersistence
{
    /// <summary>Registers the selected SQL boundary. Existing installations remain on Legacy until their cutover is explicitly configured.</summary>
    /// <param name="builder">Product host.</param>
    public static void AddQuestPersistence(this WebApplicationBuilder builder)
    {
        switch (builder.Configuration["Persistence:Mode"] ?? "Legacy")
        {
            case "Legacy":
                builder.Services.AddScoped<IQuestStore, SqlQuestStore>();
                builder.Services.AddScoped<ISyncAnchors, SqlQuestStore>();
                builder.Services.AddScoped<SqlQuestLifecycle>();
                builder.Services.AddScoped<IQuestLifecycle>(services => services.GetRequiredService<SqlQuestLifecycle>());
                break;
            case "Relational":
                var connection = builder.Configuration.GetConnectionString("quests") ?? throw new InvalidOperationException("SQL Server connection 'quests' is required.");
                builder.Services.AddScoped(_ => new RelationalQuestCommands(connection));
                builder.Services.AddScoped<RelationalQuestStore>();
                builder.Services.AddScoped<IQuestStore>(services => services.GetRequiredService<RelationalQuestStore>());
                builder.Services.AddScoped<ISyncAnchors>(services => services.GetRequiredService<RelationalQuestStore>());
                builder.Services.AddScoped<IQuestLifecycle, RelationalQuestLifecycle>();
                builder.Services.AddHostedService<LifecycleMaintenance>();
                if (builder.Configuration.GetValue("Persistence:ReconciliationEnabled", true))
                    builder.Services.AddHostedService<RelationalMaintenance>();
                break;
            default:
                throw new InvalidOperationException("Persistence:Mode must be Legacy or Relational.");
        }
    }
}
