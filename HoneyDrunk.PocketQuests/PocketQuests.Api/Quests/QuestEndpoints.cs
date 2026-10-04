using PocketQuests.Api.Contracts.Catalogs;
using PocketQuests.Api.Contracts.Commands;
using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests;
using PocketQuests.Api.Contracts.Schedules;
using PocketQuests.Api.Contracts.Synchronization;
using PocketQuests.Api.Hosting;
using PocketQuests.Application.Quests;
using PocketQuests.Application.Synchronization;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Accounts;
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
            (await anchors.CreateAnchor(Identity(principal), request.DeviceId, request.BootId, request.DeviceUtc, clock.GetUtcNow(), token)).ToContract())
            .WithName("CreateSyncAnchor").ProducesProblem(503).Produces(404);
        api.MapGet("/planning/clock", (string date, string time, string zone) => Scheduling.Planned(date, time, zone).ToContract()).WithName("PlanClock");
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
            [.. Catalog.Categories.Select(item => item.ToContract())],
            [.. Catalog.Attributes.Select(item => item.ToContract())],
            [.. Catalog.Skills.Select(item => item.ToContract())],
            [.. Catalog.Quests.Select(item => item.ToContract())],
            [.. Progression.Rules.Select(item => item.ToContract())])).WithName("GetCatalog");
        api.MapPost("/profile", async (InitializeProfile request, ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            (await service.Initialize(Identity(principal), request.Zone, token)).ToContract()).WithName("InitializeProfile").ProducesProblem(409).ProducesProblem(503);
        api.MapGet("/state", async (ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            (await service.Read(Identity(principal), token)).ToContract()).WithName("GetState").Produces(404).ProducesProblem(503);
        api.MapPost("/commands", async (QuestCommand command, ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            (await service.Execute(Identity(principal), command.ToDomain(), token)).ToContract())
            .WithName("ExecuteCommand").RequireRateLimiting(ApiHttpPolicy.Commands).Produces(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(503);
    }

    private static AccountIdentity Identity(ClaimsPrincipal principal) => new(
        principal.FindFirstValue("iss") ?? throw new UnauthorizedAccessException(),
        principal.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());
}
