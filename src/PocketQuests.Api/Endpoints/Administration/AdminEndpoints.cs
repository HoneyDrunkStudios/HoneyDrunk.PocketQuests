using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Api.Administration;
using PocketQuests.Api.Hosting;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Services.Administration;

namespace PocketQuests.Api.Endpoints.Administration;

/// <summary>Configured-operator administration and same-origin web session routes.</summary>
public static class AdminEndpoints
{
    /// <summary>Maps operator-only directory and audited moderation routes.</summary>
    /// <param name="app">The API host.</param>
    public static void MapAdministration(this WebApplication app)
    {
        app.MapGet("/admin", Home).ExcludeFromDescription();
        app.MapGet("/admin/login", Login).ExcludeFromDescription();
        var admin = app.MapGroup("/api/admin").WithTags("PocketQuests.Admin")
            .RequireAuthorization(AdminWeb.Policy).RequireRateLimiting(ApiHttpPolicy.Commands);
        admin.MapGet("/session", Session).ExcludeFromDescription();
        admin.MapPost("/logout", (Delegate)Logout).AddEndpointFilter<AdminMutationFilter>().ExcludeFromDescription();
        admin.MapPost("/users/search", Users).AddEndpointFilter<AdminMutationFilter>().WithName("SearchAdminUsers").ProducesProblem(400).Produces(401).Produces(403).ProducesProblem(429).ProducesProblem(500);
        admin.MapPost("/users/access", SetAccess).AddEndpointFilter<AdminMutationFilter>().WithName("SetAdminUserAccess")
            .ProducesProblem(400).Produces(401).Produces(403).Produces(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(500);
    }

    private static RedirectHttpResult Home() => TypedResults.Redirect("/admin/index.html");

    private static async Task<IResult> Login(IAuthenticationSchemeProvider schemes)
    {
        if (await schemes.GetSchemeAsync(AdminWeb.Oidc) is null)
            return Results.Problem("Administrative sign-in is not configured.", statusCode: 503);
        return Results.Challenge(new AuthenticationProperties { RedirectUri = "/admin/index.html" }, [AdminWeb.Oidc]);
    }

    private static IResult Session(HttpContext context, IAntiforgery antiforgery) =>
        Results.Ok(new { userId = context.User.FindFirst("sub")?.Value, csrfToken = antiforgery.GetAndStoreTokens(context).RequestToken });

    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.SignOutAsync(AdminWeb.Cookie);
        return Results.NoContent();
    }

    private static async Task<Ok<AdminUserPage>> Users(SearchUsersRequest request, IAdminUserService service, CancellationToken token) =>
        TypedResults.Ok(await service.Search(request.Query, request.After, token));

    private static async Task<Ok<AdminUser>> SetAccess(SetUserAccessRequest request, IAdminUserService service, CancellationToken token) =>
        TypedResults.Ok(await service.SetAccess(request, token));
}
