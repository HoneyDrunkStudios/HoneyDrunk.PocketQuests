using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Synchronization;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Quests;
using PocketQuests.Application.Synchronization;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Security.Claims;

namespace PocketQuests.Api.Quests;

/// <summary>Authenticated quest feature routes; ownership comes exclusively from the verified principal.</summary>
public static class QuestEndpoints
{
    /// <summary>Registers the account-scoped quest API.</summary>
    /// <param name="app">The configured endpoint host.</param>
    public static void MapQuestEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapPost("/sync-anchor", (AnchorRequest request, ClaimsPrincipal principal, ISyncAnchors anchors, TimeProvider clock, CancellationToken token) =>
            anchors.CreateAnchor(Identity(principal), request.DeviceId, request.BootId, request.DeviceUtc, clock.GetUtcNow(), token));
        api.MapGet("/planning/clock", (string date, string time, string zone) => Scheduling.Planned(date, time, zone));
        api.MapGet("/planning/zone", async (string zone, ClaimsPrincipal principal, QuestService service, TimeProvider clock, CancellationToken token) =>
        {
            var selected = Scheduling.Zone(zone).Id;
            var state = await service.Read(Identity(principal), "UTC", token);
            var now = clock.GetUtcNow();
            return new ZonePreview(state.Zone, selected, [.. state.Occurrences.Where(o => (o.Status is QuestStatus.Active or QuestStatus.Frozen or QuestStatus.Offered) && o.Occurrence.DueDate is not null && (o.Status == QuestStatus.Frozen || o.Occurrence.Deadline is null || o.Occurrence.Deadline > now)).Select(o =>
            {
                var deadline = Scheduling.Deadline(Scheduling.ParseDate(o.Occurrence.DueDate!), selected);
                return new DeadlineChange(o.Occurrence.Id, o.Occurrence.Quest.Title, deadline, o.Status == QuestStatus.Active && deadline <= now);
            })]);
        });
        api.MapGet("/catalog", () => Results.Ok(new { Catalog.Categories, Catalog.Attributes, Catalog.Skills, Catalog.Quests, Progression.Rules }));
        api.MapPost("/profile", (InitializeProfile request, ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            service.Read(Identity(principal), request.Zone, token));
        api.MapGet("/state", (ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            service.Read(Identity(principal), "UTC", token));
        api.MapPost("/commands", (QuestCommand command, ClaimsPrincipal principal, QuestService service, CancellationToken token) =>
            service.Execute(Identity(principal), command, token));
    }

    private static AccountIdentity Identity(ClaimsPrincipal principal) => new(
        principal.FindFirstValue("iss") ?? throw new UnauthorizedAccessException(),
        principal.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());
}
