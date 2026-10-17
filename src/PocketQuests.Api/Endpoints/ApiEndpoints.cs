using PocketQuests.Api.Endpoints.Catalogs;
using PocketQuests.Api.Endpoints.Exports;
using PocketQuests.Api.Endpoints.Profiles;
using PocketQuests.Api.Endpoints.Quests;
using PocketQuests.Api.Endpoints.Schedules;
using PocketQuests.Api.Endpoints.Synchronization;

namespace PocketQuests.Api.Endpoints;

/// <summary>Composes the public endpoint groups.</summary>
public static class ApiEndpoints
{
    /// <summary>Registers the existing authenticated product routes.</summary>
    /// <param name="app">The API host.</param>
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapSynchronization();
        app.MapPlanning();
        app.MapCatalog();
        app.MapProfiles();
        app.MapQuestState();
        app.MapQuestCommands();
        app.MapExportEndpoints();
    }
}
