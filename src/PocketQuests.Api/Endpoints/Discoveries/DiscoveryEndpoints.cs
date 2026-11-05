using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Api.Hosting;
using PocketQuests.Contracts.Models.Discoveries;
using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Contracts.Responses.Discoveries;
using PocketQuests.Services.Discoveries;
using System.Collections.Immutable;

namespace PocketQuests.Api.Endpoints.Discoveries;

/// <summary>Authenticated, rate-limited private discovery journal endpoints.</summary>
public static class DiscoveryEndpoints
{
    /// <summary>Registers bounded journal reads and explicit conditional edits.</summary>
    /// <param name="app">API host.</param>
    public static void MapDiscoveries(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("PocketQuests.Api").RequireAuthorization().RequireRateLimiting(ApiHttpPolicy.Commands);
        group.MapGet("/discoveries", Read).WithName("GetDiscoveries").ProducesProblem(400).Produces(404).ProducesProblem(429).ProducesProblem(500);
        group.MapPost("/discoveries/save", Save).WithName("SaveDiscovery").ProducesProblem(400).Produces(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(500);
        group.MapPost("/discoveries/remove", Remove).WithName("RemoveDiscovery").ProducesProblem(400).Produces(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(500);
    }

    private static async Task<Ok<ImmutableArray<SavedPlace>>> Read(ISavedPlaceService service, CancellationToken token) => TypedResults.Ok(await service.Read(token));

    private static async Task<Ok<SavedPlace>> Save(SavePlaceRequest request, ISavedPlaceService service, CancellationToken token) => TypedResults.Ok(await service.Save(request, token));

    private static async Task<Ok<RemovalResult>> Remove(RemovePlaceRequest request, ISavedPlaceService service, CancellationToken token) => TypedResults.Ok(await service.Remove(request, token));
}
