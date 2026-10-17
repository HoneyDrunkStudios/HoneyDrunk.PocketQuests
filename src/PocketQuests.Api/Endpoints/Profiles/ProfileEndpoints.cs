using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Contracts.Requests.Profiles;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Services.Profiles;

namespace PocketQuests.Api.Endpoints.Profiles;

/// <summary>Authenticated explicit profile initialization.</summary>
public static class ProfileEndpoints
{
    /// <summary>Registers the existing profile route.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapProfiles(this WebApplication app) => app.MapPost("/api/profile", Initialize)
        .WithName("InitializeProfile").WithTags("PocketQuests.Api").RequireAuthorization()
        .ProducesProblem(400).ProducesProblem(500).ProducesProblem(409).ProducesProblem(503);

    private static async Task<Ok<QuestState>> Initialize(InitializeProfile request, IProfileService service, CancellationToken token) =>
        TypedResults.Ok(await service.Initialize(request, token));
}
