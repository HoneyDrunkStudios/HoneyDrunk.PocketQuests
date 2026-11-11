using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using System.Net;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <content>Immutable studio drafts and server-authoritative preview against actual HTTP and deployed SQL.</content>
public sealed partial class RelationalApiTests
{
    /// <summary>Preview does not save, unauthorized callers cannot author, and stale saves preserve prior content.</summary>
    /// <returns>Completion after immutable draft and pointer-ownership assertions.</returns>
    [Fact]
    public async Task SystemQuestDraftsValidatePreviewRetainRevisionsAndRejectStaleEditors()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        using var ordinary = host.Client();
        await Setup(admin);
        var id = Guid.NewGuid();
        var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N"), Title = "  Studio adventure  " };
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/quests/preview", new { id, quest }, Json)).StatusCode);
        using var preview = await admin.PostAsJsonAsync("/api/admin/quests/preview", new { id, quest }, Json);
        preview.EnsureSuccessStatusCode();
        var terms = await preview.Content.ReadFromJsonAsync<PocketQuests.Contracts.Models.Quests.Quest>(Json);
        Assert.Equal("Studio adventure", terms!.Title);
        Assert.Equal(quest.BaseXp, terms.BaseXp);
        await using var db = fixture.Context();
        Assert.False(await db.SystemQuest.AnyAsync(row => row.Id == quest.Id));
        var first = await SaveDraft(admin, id, null, quest);
        var retry = await SaveDraft(admin, id, null, quest);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first, Json), System.Text.Json.JsonSerializer.Serialize(retry, Json));
        Assert.Null(first.Published);
        Assert.Equal(1, first.DraftNumber);
        var second = await SaveDraft(admin, id, first.Version, quest with { Title = "Edited studio adventure" });
        Assert.Equal(2, second.DraftNumber);
        Assert.NotEqual(first.Version, second.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/quests/draft", new { id, expectedVersion = first.Version, quest }, Json)).StatusCode);
        var retained = await db.SystemQuestRevision.Where(row => row.SystemQuestId == quest.Id).OrderBy(row => row.Version).ToListAsync();
        Assert.Equal(2, retained.Count);
        Assert.Contains("Studio adventure", retained[0].SnapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Edited studio adventure", retained[0].SnapshotJson, StringComparison.Ordinal);
        var studio = await Studio(admin);
        Assert.Equal(second.Version, studio.Quests.Single(row => row.Id == id).Version);
        var otherId = Guid.NewGuid();
        var other = await SaveDraft(admin, otherId, null, quest with { Id = "PQ-ADM-" + otherId.ToString("N") });
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.SystemQuest SET PublishedSystemQuestRevisionId='{other.DraftSystemQuestRevisionId:D}' WHERE Id='{quest.Id}';"));
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.SystemQuestRevision SET Version=0 WHERE Id='{first.DraftSystemQuestRevisionId:D}';"));
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.SystemQuestRevision SET SnapshotJson=N'not json' WHERE Id='{first.DraftSystemQuestRevisionId:D}';"));
        var tracked = await db.SystemQuestRevision.AsTracking().SingleAsync(row => row.Id == first.DraftSystemQuestRevisionId);
        tracked.SnapshotJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    /// <summary>Public template content cannot capture account-owned skills or bypass allocation and content bounds.</summary>
    /// <returns>Completion after invalid input cases and a clean no-write result.</returns>
    [Fact]
    public async Task SystemQuestPreviewRejectsPrivateOrInvalidTermsWithoutWriting()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        var id = Guid.NewGuid();
        var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N") };
        foreach (var invalid in new[] { quest with { IsCustom = true }, quest with { Title = " " }, quest with { CategoryId = "private" }, quest with { Skills = [new(Guid.NewGuid().ToString(), 10000)] }, quest with { Attributes = [new("a01", 99)] }, quest with { Description = new string('x', 2001) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/quests/preview", new { id, quest = invalid }, Json)).StatusCode);
        await using var db = fixture.Context();
        Assert.False(await db.SystemQuest.AnyAsync(row => row.Id == quest.Id));
    }

    /// <summary>Malformed allocation elements return validation errors before input mapping can dereference them.</summary>
    /// <returns>Completion after both authoring routes reject both malformed pools without writes.</returns>
    [Fact]
    public async Task SystemQuestInputsRejectNullAllocationElementsBeforeMapping()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        await Setup(admin);
        var id = Guid.NewGuid();
        var input = System.Text.Json.JsonSerializer.SerializeToNode(Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N") }, Json)!;
        foreach (var pool in new[] { "attributes", "skills" })
        {
            var malformed = input.DeepClone();
            malformed[pool] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
            foreach (var route in new[] { "preview", "draft" })
                Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/quests/" + route, new { id, quest = malformed }, Json)).StatusCode);
        }

        await using var db = fixture.Context();
        Assert.False(await db.SystemQuest.AnyAsync(row => row.Id == "PQ-ADM-" + id.ToString("N")));
    }

    /// <summary>Erasing the operator removes personal stamps and audit ownership while retaining public template history.</summary>
    /// <returns>Completion after the verified lifecycle erasure.</returns>
    [Fact]
    public async Task SystemQuestPublicHistorySurvivesOperatorErasureWithoutActorIdentifiers()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        await Setup(admin);
        var id = Guid.NewGuid();
        var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N") };
        var saved = await SaveDraft(admin, id, null, quest);
        var intent = new LifecycleIntent(actor, 2, IdentityProtocol.Erasing, Start.AddDays(1), Start.AddDays(1).AddHours(1), IdentityProtocol.ConsumerId, new string('c', 64), Start);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", Start.AddDays(1));
        await using var db = fixture.Context();
        var head = await db.SystemQuest.SingleAsync(row => row.Id == quest.Id);
        Assert.Null(head.CreatedBy);
        Assert.Null(head.ModifiedBy);
        Assert.True(await db.SystemQuestRevision.AnyAsync(row => row.Id == saved.DraftSystemQuestRevisionId));
        Assert.False(await db.Set<HoneyDrunk.Audit.Data.AuditRecord>().AnyAsync(row => row.Actor == actor));
    }

    /// <summary>Publishing controls new acceptance while frozen receipts, rewards and recurrence retain their original terms.</summary>
    /// <returns>Completion after edit, republish, withdrawal, process restart, replay, completion and Undo.</returns>
    [Fact]
    public async Task SystemQuestPublicationPreservesAcceptedRewardsRecurringTermsAndReceiptReplay()
    {
        var actor = NewOwner();
        var playerId = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        using var player = host.Client(playerId);
        await Setup(admin);
        await Setup(player);
        var id = Guid.NewGuid();
        var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N"), Title = "Original published walk" };
        var first = await SaveDraft(admin, id, null, quest);
        Assert.DoesNotContain((await PublicCatalog(player)).Quests, row => row.Id == quest.Id);
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), quest.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync("/api/commands", accept, Json)).StatusCode);
        var live = await PublishDraft(admin, first, true);
        Assert.Equal(first.DraftSystemQuestRevisionId, live.PublishedSystemQuestRevisionId);
        var accepted = await Command(player, accept);
        var seriesCommand = new QuestCommand(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1);
        var recurring = await Command(player, seriesCommand);
        Assert.Equal(quest.Title, recurring.Schedule.Series.Single().Quest.Title);
        var edited = await SaveDraft(admin, id, live.Version, quest with { Title = "Changed live terms", Effort = Effort.Large });
        Assert.Equal(quest.Title, (await PublicCatalog(player)).Quests.Single(row => row.Id == quest.Id).Title);
        Assert.Equal(first.DraftSystemQuestRevisionId, edited.PublishedSystemQuestRevisionId);
        var republished = await PublishDraft(admin, edited, true);
        Assert.Equal("Changed live terms", (await PublicCatalog(player)).Quests.Single(row => row.Id == quest.Id).Title);
        var later = await Command(player, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), quest.Id, AcceptedQuest: quest with { Title = "Forged client title", Effort = Effort.Small }));
        Assert.Equal(quest.Title, later.Occurrences.Single(row => row.Occurrence.Id == accept.OccurrenceId).Occurrence.Quest.Title);
        Assert.Equal(quest.Title, later.Schedule.Series.Single().Quest.Title);
        Assert.Contains(later.Occurrences, row => row.Occurrence.Quest.Title == "Changed live terms" && row.Occurrence.Quest.BaseXp > quest.BaseXp);
        var completeCommand = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var completed = await Command(player, completeCommand);
        Assert.Equal(quest.BaseXp, completed.OverallXp);
        await Command(player, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: completed.Occurrences.Single(row => row.Occurrence.Id == accept.OccurrenceId).Completion!.Id));
        await PublishDraft(admin, republished, false);
        Assert.DoesNotContain((await PublicCatalog(player)).Quests, row => row.Id == quest.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync("/api/commands", accept with { OperationId = Guid.NewGuid(), OccurrenceId = Guid.NewGuid() }, Json)).StatusCode);
        using var restarted = new Host(fixture.Connection, adminUserId: actor);
        using var returning = restarted.Client(playerId);
        Assert.Equal(JsonSerializer.Serialize(accepted, Json), JsonSerializer.Serialize(await Command(returning, accept), Json));
        Assert.Equal(JsonSerializer.Serialize(recurring, Json), JsonSerializer.Serialize(await Command(returning, seriesCommand), Json));
        Assert.Equal(JsonSerializer.Serialize(completed, Json), JsonSerializer.Serialize(await Command(returning, completeCommand), Json));
        Assert.Equal(0, (await Read(returning)).OverallXp);
    }

    /// <summary>Only the configured operator can publish the exact reviewed head and saved revision.</summary>
    /// <returns>Completion after unauthorized and stale publication attempts leave visibility unchanged.</returns>
    [Fact]
    public async Task SystemQuestPublishingRejectsUnauthorizedAndStaleReviewWithoutLeakingDrafts()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        using var ordinary = host.Client();
        await Setup(admin);
        var id = Guid.NewGuid();
        var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N") };
        var first = await SaveDraft(admin, id, null, quest);
        var review = new { id, expectedVersion = first.Version, expectedDraftRevisionId = first.DraftSystemQuestRevisionId, published = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/quests/publish", review, Json)).StatusCode);
        var second = await SaveDraft(admin, id, first.Version, quest with { Title = "Private edited title" });
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/quests/publish", review, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/quests/publish", new { id, expectedVersion = second.Version, expectedDraftRevisionId = first.DraftSystemQuestRevisionId, published = true }, Json)).StatusCode);
        Assert.DoesNotContain((await PublicCatalog(ordinary)).Quests, row => row.Id == quest.Id);
        var live = await PublishDraft(admin, second, true);
        Assert.Equal("Private edited title", live.Published!.Title);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/quests/publish", new { id, expectedVersion = second.Version, expectedDraftRevisionId = second.DraftSystemQuestRevisionId, published = false }, Json)).StatusCode);
        Assert.Contains((await PublicCatalog(ordinary)).Quests, row => row.Id == quest.Id);
    }

    /// <summary>Schedule-only edits keep account-owned terms, including penalty consent, after republishing and withdrawal.</summary>
    /// <param name="penalty">Original optional penalty percentage.</param>
    /// <returns>Completion after schedule edits, stale rejection, new-series denial and exact replay.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task SystemQuestSeriesEditsRetainAcceptedTermsAfterRepublishingAndWithdrawal(int penalty)
    {
        var actor = NewOwner();
        var playerId = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        using var player = host.Client(playerId);
        await Setup(admin);
        await Setup(player);
        var id = Guid.NewGuid();
        var original = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N"), Title = "Accepted recurring terms", PenaltyPercent = penalty };
        var live = await PublishDraft(admin, await SaveDraft(admin, id, null, original), true);
        var create = new QuestCommand(
            Guid.NewGuid(),
            QuestActions.SaveSeries,
            QuestId: original.Id,
            DueDate: "2026-01-01",
            ExpectedRevision: 0,
            SeriesId: Guid.NewGuid(),
            Cadence: Cadence.Days,
            Interval: 1,
            ConfirmPenalty: penalty != 0,
            AcceptedLoss: penalty == 0 ? null : ((original.BaseXp * penalty) + 50) / 100,
            AcceptedQuest: penalty == 0 ? null : original);
        var created = await Command(player, create);
        var replacement = await SaveDraft(admin, id, live.Version, original with { Title = "Republished different terms", Effort = Effort.Large, PenaltyPercent = 50 });
        live = await PublishDraft(admin, replacement, true);
        var edit = create with { OperationId = Guid.NewGuid(), ExpectedRevision = 1, Interval = 2 };
        var edited = await Command(player, edit);
        Assert.Equal(JsonSerializer.Serialize(original, Json), JsonSerializer.Serialize(edited.Schedule.Series.Single().Quest, Json));
        await PublishDraft(admin, live, false);
        var withdrawnEdit = edit with { OperationId = Guid.NewGuid(), ExpectedRevision = 2, Interval = 3 };
        var afterWithdrawal = await Command(player, withdrawnEdit);
        var series = afterWithdrawal.Schedule.Series.Single();
        Assert.Equal(JsonSerializer.Serialize(original, Json), JsonSerializer.Serialize(series.Quest, Json));
        Assert.Equal(3, series.Version);
        Assert.Equal(3, series.Interval);
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync("/api/commands", edit with { OperationId = Guid.NewGuid() }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync("/api/commands", create with { OperationId = Guid.NewGuid(), SeriesId = Guid.NewGuid() }, Json)).StatusCode);
        using var restarted = new Host(fixture.Connection, adminUserId: actor);
        using var returning = restarted.Client(playerId);
        Assert.Equal(JsonSerializer.Serialize(created, Json), JsonSerializer.Serialize(await Command(returning, create), Json));
        Assert.Equal(JsonSerializer.Serialize(edited, Json), JsonSerializer.Serialize(await Command(returning, edit), Json));
        Assert.Equal(JsonSerializer.Serialize(afterWithdrawal, Json), JsonSerializer.Serialize(await Command(returning, withdrawnEdit), Json));
    }

    private static async Task<AdminSystemQuest> PublishDraft(HttpClient client, AdminSystemQuest reviewed, bool published)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/quests/publish", new { reviewed.Id, expectedVersion = reviewed.Version, expectedDraftRevisionId = reviewed.DraftSystemQuestRevisionId, published }, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminSystemQuest>(Json))!;
    }

    private static async Task<PocketQuests.Contracts.Responses.Catalogs.CatalogResponse> PublicCatalog(HttpClient client) =>
        (await client.GetFromJsonAsync<PocketQuests.Contracts.Responses.Catalogs.CatalogResponse>("/api/catalog", Json))!;

    private static async Task<AdminSystemQuest> SaveDraft(HttpClient client, Guid id, string? expectedVersion, PocketQuests.Domain.Models.Quests.Quest quest)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/quests/draft", new { id, expectedVersion, quest }, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminSystemQuest>(Json))!;
    }

    private static async Task<SystemQuestStudio> Studio(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/quests/list", new { });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SystemQuestStudio>(Json))!;
    }
}
