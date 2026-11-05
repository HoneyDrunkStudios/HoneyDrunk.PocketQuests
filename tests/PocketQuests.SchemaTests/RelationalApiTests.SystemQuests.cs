using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Accounts;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Immutable studio drafts and server-authoritative preview against actual HTTP and deployed SQL.</summary>
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
