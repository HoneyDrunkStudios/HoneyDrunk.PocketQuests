using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Contracts.Models.Synchronization;
using PocketQuests.Contracts.Requests.Synchronization;
using PocketQuests.Services.Synchronization;

namespace PocketQuests.Api.Endpoints.Synchronization;

/// <summary>Authenticated recorded-time proof issuance.</summary>
public static class SynchronizationEndpoints
{
    /// <summary>Registers the existing sync-anchor route.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapSynchronization(this WebApplication app) => app.MapPost("/api/sync-anchor", Create)
        .WithName("CreateSyncAnchor").WithTags("PocketQuests.Api").RequireAuthorization()
        .ProducesProblem(400).ProducesProblem(500).ProducesProblem(503).Produces(404);

    private static async Task<Ok<SyncAnchor>> Create(AnchorRequest request, ISynchronizationService service, CancellationToken token) =>
        TypedResults.Ok(await service.CreateAnchor(request, token));
}
