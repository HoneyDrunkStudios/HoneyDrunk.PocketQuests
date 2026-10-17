using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Contracts.Models.Schedules;
using PocketQuests.Services.Schedules;

namespace PocketQuests.Api.Endpoints.Schedules;

/// <summary>Authenticated calendar previews with no persistence decisions in the endpoint.</summary>
public static class PlanningEndpoints
{
    /// <summary>Registers the existing planning routes.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapPlanning(this WebApplication app)
    {
        var planning = app.MapGroup("/api/planning").WithTags("PocketQuests.Api").RequireAuthorization().ProducesProblem(400).ProducesProblem(500);
        planning.MapGet("/clock", Clock).WithName("PlanClock");
        planning.MapGet("/zone", Zone).WithName("PreviewZone").Produces(404).ProducesProblem(503);
    }

    private static Ok<PlannedMoment> Clock(string date, string time, string zone, IPlanningService service) => TypedResults.Ok(service.PlanClock(date, time, zone));

    private static async Task<Ok<ZonePreview>> Zone(string zone, IPlanningService service, CancellationToken token) => TypedResults.Ok(await service.PreviewZone(zone, token));
}
