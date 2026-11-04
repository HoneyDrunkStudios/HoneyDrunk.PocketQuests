using Microsoft.EntityFrameworkCore;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Domain.Catalogs;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Bounded immutable history and restoration through the existing draft workflow.</summary>
public sealed partial class RelationalApiTests
{
    /// <summary>History is operator-only, paginates stably through new revisions and never moves publication.</summary>
    /// <returns>Completion after a retained revision is explicitly saved as new content.</returns>
    [Fact]
    public async Task SystemQuestHistoryPagesRetainedContentAndRestoresAsNewDraftWithoutPublishing()
    {
        var actor = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: actor);
        using var admin = host.Client(actor);
        using var ordinary = host.Client();
        using var anonymous = host.CreateClient();
        await Setup(admin);
        var id = Guid.NewGuid();
        var terms = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N"), Title = "Original retained adventure" };
        var first = await SaveDraft(admin, id, null, terms);
        var head = await PublishDraft(admin, first, true);
        for (var number = 2; number <= 22; number++)
            head = await SaveDraft(admin, id, head.Version, terms with { Title = "Revision " + number });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/quests/history", new { id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/quests/history", new { id })).StatusCode);
        foreach (var before in new[] { 0, -1, 1002 })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/quests/history", new { id, before })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/quests/history", new { id = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/quests/history", new { id = Guid.NewGuid() })).StatusCode);

        var newest = await History(admin, id);
        Assert.Equal(id, newest.Id);
        Assert.Equal(Enumerable.Range(3, 20).Reverse(), newest.Revisions.Select(row => row.Number));
        Assert.Equal(3, newest.NextBefore);

        // A new append must not skip or duplicate the older page.
        head = await SaveDraft(admin, id, head.Version, terms with { Title = "Revision 23" });
        var older = await History(admin, id, newest.NextBefore);
        Assert.Equal(new[] { 2, 1 }, older.Revisions.Select(row => row.Number));
        Assert.Null(older.NextBefore);
        var original = older.Revisions.Single(row => row.Number == 1);
        Assert.Equal(first.DraftSystemQuestRevisionId, original.Id);
        Assert.Equal(terms.Title, original.Quest.Title);
        Assert.Empty((await History(admin, id, 1)).Revisions);
        Assert.Equal(head.Version, (await Studio(admin)).Quests.Single(row => row.Id == id).Version);

        var restored = await SaveDraft(admin, id, head.Version, terms);
        Assert.Equal(24, restored.DraftNumber);
        Assert.NotEqual(original.Id, restored.DraftSystemQuestRevisionId);
        Assert.Equal(first.DraftSystemQuestRevisionId, restored.PublishedSystemQuestRevisionId);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/quests/draft", new { id, expectedVersion = head.Version, quest = terms }, Json)).StatusCode);
        await using var db = fixture.Context();
        Assert.Equal(24, await db.SystemQuestRevision.CountAsync(row => row.SystemQuestId == terms.Id));
        Assert.Equal(terms.Title, (await History(admin, id, 2)).Revisions.Single().Quest.Title);
    }

    private static async Task<SystemQuestHistory> History(HttpClient client, Guid id, int? before = null)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/quests/history", new { id, before }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SystemQuestHistory>(Json))!;
    }
}
