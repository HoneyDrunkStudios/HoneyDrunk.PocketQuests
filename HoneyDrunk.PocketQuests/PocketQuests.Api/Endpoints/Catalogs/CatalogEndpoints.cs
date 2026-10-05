using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Contracts.Responses.Catalogs;
using PocketQuests.Services.Catalogs;

namespace PocketQuests.Api.Endpoints.Catalogs;

/// <summary>Authenticated canonical catalog reads.</summary>
public static class CatalogEndpoints
{
    /// <summary>Registers the existing catalog route.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapCatalog(this WebApplication app) => app.MapGet("/api/catalog", Read)
        .WithName("GetCatalog").WithTags("PocketQuests.Api").RequireAuthorization().ProducesProblem(400).ProducesProblem(500);

    private static Ok<CatalogResponse> Read(ICatalogService service) => TypedResults.Ok(service.Read());
}
