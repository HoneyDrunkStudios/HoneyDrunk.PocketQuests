using PocketQuests.Application.Quests;
using PocketQuests.Application.Synchronization;
using PocketQuests.Contracts.Models.Schedules;
using PocketQuests.Contracts.Requests.Profiles;
using PocketQuests.Contracts.Requests.Synchronization;
using PocketQuests.Contracts.Responses.Catalogs;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Schedules.Mapping;
using PocketQuests.Services.Synchronization.Mapping;
using System.Security.Claims;
using Progression = PocketQuests.Domain.Progress.Progression;
using QuestStatus = PocketQuests.Domain.Models.Quests.QuestStatus;
using Scheduling = PocketQuests.Domain.Schedules.Scheduling;

namespace PocketQuests.Api.Quests;

/// <summary>Authenticated quest feature routes; ownership comes exclusively from the verified principal.</summary>
public static class QuestEndpoints
{
    /// <summary>Registers the account-scoped quest API.</summary>
    /// <param name="app">The configured endpoint host.</param>
    public static void MapQuestEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().ProducesProblem(400).ProducesProblem(500);
        api.MapPost("/sync-anchor", async (AnchorRequest request, ClaimsPrincipal principal, ISyncAnchors anchors, TimeProvider clock, CancellationToken token) =>
            (await anchors.CreateAnchor(Identity(principal), request.DeviceId, request.BootId, request.DeviceUtc, clock.GetUtcNow(), token)).ToModel())
            .WithName("CreateSyncAnchor").ProducesProblem(503).Produces(404);
        api.MapGet("/planning/clock", (string date, string time, string zone) => Scheduling.Planned(date, time, zone).ToModel()).WithName("PlanClock");
        api.MapGet("/planning/zone", async (string zone, ClaimsPrincipal principal, QuestService service, TimeProvider clock, CancellationToken token) =>
        {
            var selected = Scheduling.Zone(zone).Id;
            var state = await service.Read(Identity(principal), token);
            var now = clock.GetUtcNow();
            return new ZonePreview(state.Zone, selected, [.. state.Occurrences.Where(o => (o.Status is QuestStatus.Active or QuestStatus.Frozen or QuestStatus.Offered) && o.Occurrence.DueDate is not null && (o.Status == QuestStatus.Frozen || o.Occurrence.Deadline is null || o.Occurrence.Deadline > now)).Select(o =>
            {
                var deadline = Scheduling.Deadline(Scheduling.ParseDate(o.Occurrence.DueDate!), selected);
                return new DeadlineChange(o.Occurrence.Id, o.Occurrence.Quest.Title, deadline, o.Status == QuestStatus.Active && deadline <= now);
            })]);
        }).WithName("PreviewZone").Produces(404).ProducesProblem(503);
        api.MapGet("/catalog", () => new CatalogResponse(
            [.. Catalog.Categories.Select(item => item.ToModel())],
            [.. Catalog.Attributes.Select(item => item.ToModel())],
            [.. Catalog.Skills.Select(item => item.ToModel())],
            [.. Catalog.Quests.Select(item => item.ToModel())],
            [.. Progression.Rules.Select(item => item.ToModel())])).WithName("GetCatalog");
        api.MapPost("/profile", async (InitializeProfile request, ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            (await service.Initialize(Identity(principal), request.Zone, token)).ToModel()).WithName("InitializeProfile").ProducesProblem(409).ProducesProblem(503);
        api.MapGet("/state", async (ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            (await service.Read(Identity(principal), token)).ToModel()).WithName("GetState").Produces(404).ProducesProblem(503);
    }

    private static AccountIdentity Identity(ClaimsPrincipal principal) => new(
        principal.FindFirstValue("iss") ?? throw new UnauthorizedAccessException(),
        principal.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());
}
