using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Api.Hosting;
using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Services.Quests;

namespace PocketQuests.Api.Endpoints.Quests;

/// <summary>Public quest command routing and typed HTTP results.</summary>
public static class QuestEndpoints
{
    /// <summary>Registers the existing command route without changing its wire contract.</summary>
    /// <param name="app">The API host.</param>
    public static void MapQuestCommands(this WebApplication app) => app.MapPost("/api/commands", Execute)
        .WithName("ExecuteCommand").WithTags("PocketQuests.Api").RequireAuthorization().RequireRateLimiting(ApiHttpPolicy.Commands)
        .ProducesProblem(400).Produces(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503);

    /// <summary>Registers the existing read-only state route.</summary>
    /// <param name="app">The API host.</param>
    public static void MapQuestState(this WebApplication app) => app.MapGet("/api/state", Read)
        .WithName("GetState").WithTags("PocketQuests.Api").RequireAuthorization()
        .ProducesProblem(400).ProducesProblem(500).Produces(404).ProducesProblem(503);

    private static async Task<Ok<QuestState>> Read(IQuestService service, CancellationToken token) =>
        TypedResults.Ok(await service.Read(token));

    private static async Task<Ok<QuestState>> Execute(QuestCommand request, IQuestService service, CancellationToken token) =>
        TypedResults.Ok(await service.Execute(request, token));
}
