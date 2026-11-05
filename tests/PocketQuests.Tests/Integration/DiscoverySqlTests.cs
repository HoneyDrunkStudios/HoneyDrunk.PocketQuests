using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Contracts.Models.Discoveries;
using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Discoveries;
using PocketQuests.Domain.Commands;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Discovery API acceptance against the production DACPAC and real SQL.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Saved notes, visit status and quest linkage survive a fresh API host without granting progress.</summary>
    /// <returns>Completion of persistence, ownership and export checks.</returns>
    [Fact]
    public async Task DiscoveriesSurviveRestartAndExportWithoutAwardingXp()
    {
        var request = DiscoveryRequest();
        Guid occurrence;
        using (var host = new Host(Connection))
        using (var alice = host.Client("discovery-alice"))
        {
            await Setup(alice);
            occurrence = Assert.Single((await Command(alice, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q07"))).Occurrences).Occurrence.Id;
            request = request with { Notes = "=private note", Visited = true, QuestOccurrenceId = occurrence };
            var saved = await SaveDiscovery(alice, request);
            Assert.Equal(1, saved.Revision);
            Assert.NotNull(saved.VisitedAt);
            Assert.Equal(0, (await Read(alice)).OverallXp);
            using var replay = await alice.PostAsJsonAsync("/api/discoveries/save", request, Json);
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        }

        await Republish();
        using (var host = new Host(Connection))
        using (var alice = host.Client("discovery-alice"))
        using (var bob = host.Client("discovery-bob"))
        {
            await Setup(bob);
            var saved = Assert.Single((await alice.GetFromJsonAsync<SavedPlace[]>("/api/discoveries", Json))!);
            Assert.Equal(request.Notes, saved.Notes);
            Assert.Equal(occurrence, saved.QuestOccurrenceId);
            Assert.Empty((await bob.GetFromJsonAsync<SavedPlace[]>("/api/discoveries", Json))!);
            using var stolen = await bob.PostAsJsonAsync("/api/discoveries/save", request with { Id = Guid.NewGuid() }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);
            using var export = await alice.GetAsync(new Uri("/api/export/json", UriKind.Relative));
            export.EnsureSuccessStatusCode();
            var json = await export.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("=private note", json.GetProperty("savedPlaces")[0].GetProperty("notes").GetString());
            using var csv = await alice.GetAsync(new Uri("/api/export/csv", UriKind.Relative));
            csv.EnsureSuccessStatusCode();
            await using var bytes = await csv.Content.ReadAsStreamAsync();
            using var archive = new System.IO.Compression.ZipArchive(bytes);
            using var reader = new StreamReader(archive.GetEntry("discoveries.csv")!.Open());
            Assert.Contains("\"'=private note\"", await reader.ReadToEndAsync(), StringComparison.Ordinal);
            var changed = await SaveDiscovery(alice, request with { ExpectedRevision = 1, Visited = false, Notes = "Updated" });
            Assert.Null(changed.VisitedAt);
            using var stale = await alice.PostAsJsonAsync("/api/discoveries/remove", new RemovePlaceRequest(request.Id, 1), Json);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var removed = await alice.PostAsJsonAsync("/api/discoveries/remove", new RemovePlaceRequest(request.Id, 2), Json);
            removed.EnsureSuccessStatusCode();
            Assert.Empty((await alice.GetFromJsonAsync<SavedPlace[]>("/api/discoveries", Json))!);
            Assert.Single((await Read(alice)).Occurrences);
        }
    }

    /// <summary>Two devices cannot overwrite each other and SQL independently rejects invalid coordinates and foreign owners.</summary>
    /// <returns>Completion of concurrency and database constraint checks.</returns>
    [Fact]
    public async Task DiscoveriesEnforceConcurrencyOwnershipAndSqlBounds()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("discovery-race");
        using var bob = host.Client("discovery-other");
        await Setup(alice);
        await Setup(bob);
        var request = DiscoveryRequest();
        await SaveDiscovery(alice, request);
        var responses = await Task.WhenAll(
            alice.PostAsJsonAsync("/api/discoveries/save", request with { ExpectedRevision = 1, Notes = "First" }, Json),
            alice.PostAsJsonAsync("/api/discoveries/save", request with { ExpectedRevision = 1, Notes = "Second" }, Json));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses)
            response.Dispose();
        var foreignQuest = Assert.Single((await Command(bob, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q07"))).Occurrences).Occurrence.Id;
        using var invalid = await alice.PostAsJsonAsync("/api/discoveries/save", request with { ExpectedRevision = 2, QuestOccurrenceId = foreignQuest }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using var db = Context();
        var coordinateError = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Read.Database.ExecuteSqlRawAsync("UPDATE [pocketquests].[SavedPlace] SET [Latitude]=91 WHERE [Id]={0}", request.Id));
        Assert.Equal(547, coordinateError.Number);
        var ownerError = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Read.Database.ExecuteSqlRawAsync("UPDATE [pocketquests].[SavedPlace] SET [QuestOccurrenceId]={0} WHERE [Id]={1}", foreignQuest, request.Id));
        Assert.Equal(547, ownerError.Number);
        var ownerId = (await db.Read.Set<SavedPlaceEntity>().SingleAsync()).AccountId;
        var at = DateTimeOffset.UtcNow;
        db.Read.Set<SavedPlaceEntity>().AddRange(Enumerable.Range(1, 99).Select(index => new SavedPlaceEntity
        {
            Id = Guid.NewGuid(), AccountId = ownerId, PlaceKey = "node/" + (index + 1000), Name = "Capacity fixture", Kind = "Parks", CategoryId = "c07", Latitude = 35, Longitude = -82, Notes = string.Empty, Revision = 1, CreatedAt = at, ModifiedAt = at,
        }));
        await db.Read.SaveChangesAsync();
        using var full = await alice.PostAsJsonAsync("/api/discoveries/save", DiscoveryRequest() with { PlaceKey = "node/99999" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Equal(100, await db.Read.Set<SavedPlaceEntity>().CountAsync());
    }

    /// <summary>Final account erasure removes private places and their action-audit ownership.</summary>
    /// <returns>Completion after the real account erasure data operation.</returns>
    [Fact]
    public async Task DiscoveriesAreRemovedWithTheirAccount()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("discovery-erasure");
        await Setup(alice);
        var request = DiscoveryRequest();
        await SaveDiscovery(alice, request);
        await using var db = Context();
        var owner = await (from place in db.Read.Set<SavedPlaceEntity>()
                           join account in db.Read.Set<PocketQuests.Data.Entities.Accounts.AccountEntity>() on place.AccountId equals account.Id
                           where place.Id == request.Id
                           select account.IdentityUserId).SingleAsync();
        using var scope = host.Services.CreateScope();
        var data = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        await data.ExecuteInTransaction(
            async token =>
        {
            await data.AcquireCommandLock(owner, token);
            await data.DeleteOwned(owner, token);
            return true;
        });
        Assert.False(await db.Read.Set<SavedPlaceEntity>().AnyAsync());
    }

    /// <summary>Only an accepted occurrence can be linked, even when an offered occurrence belongs to this account.</summary>
    /// <returns>Completion of the real API acceptance guard.</returns>
    [Fact]
    public async Task DiscoveriesRejectUnacceptedRecurringOffers()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("discovery-offer");
        await Setup(alice);
        var quest = new PocketQuests.Domain.Models.Quests.Quest(Guid.NewGuid().ToString(), "Unaccepted commitment", "Visit a garden", "c07", PocketQuests.Domain.Models.Progress.Rank.F, PocketQuests.Domain.Models.Quests.Effort.Small, [], [], true, PenaltyPercent: 50);
        await Command(alice, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
        var state = await Command(alice, new(Guid.NewGuid(), "save-series", QuestId: quest.Id, DueDate: DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), SeriesId: Guid.NewGuid(), Cadence: PocketQuests.Domain.Models.Schedules.Cadence.Days, Interval: 1, ExpectedRevision: 0));
        Assert.Equal(PocketQuests.Domain.Models.Quests.QuestStatus.Offered, Assert.Single(state.Occurrences).Status);
        var offered = Assert.Single(state.Occurrences).Occurrence.Id;
        var request = DiscoveryRequest() with { QuestOccurrenceId = offered };
        using var response = await alice.PostAsJsonAsync("/api/discoveries/save", request, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await alice.GetFromJsonAsync<SavedPlace[]>("/api/discoveries", Json))!);
    }

    /// <summary>Logical export time includes saved-place writes before recurring quest reconciliation.</summary>
    /// <returns>Completion after a server-clock rollback exports a coherent snapshot.</returns>
    [Fact]
    public async Task DiscoveryExportReconcilesAtItsActualSnapshotCutoff()
    {
        var origin = DateTimeOffset.Parse("2026-11-02T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var clock = new RecoveryClock(origin);
        using var host = new Host(Connection, clock: clock);
        using var client = host.Client("discovery-clock");
        await Setup(client);
        var quest = new PocketQuests.Domain.Models.Quests.Quest(Guid.NewGuid().ToString(), "Daily garden walk", "Spend a moment in the garden", "c07", PocketQuests.Domain.Models.Progress.Rank.F, PocketQuests.Domain.Models.Quests.Effort.Small, [], [], true);
        await Command(client, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
        await Command(client, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-11-02", SeriesId: Guid.NewGuid(), Cadence: PocketQuests.Domain.Models.Schedules.Cadence.Days, Interval: 1, ExpectedRevision: 0));
        clock.Now = origin.AddDays(3);
        var saved = await SaveDiscovery(client, DiscoveryRequest());
        clock.Now = origin;
        using var response = await client.GetAsync(new Uri("/api/export/json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var export = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(saved.ModifiedAt, export.GetProperty("snapshotCutoff").GetDateTimeOffset());
        Assert.Equal("2026-11-05", export.GetProperty("state").GetProperty("today").GetString());
        Assert.Contains(export.GetProperty("state").GetProperty("occurrences").EnumerateArray(), item => item.GetProperty("occurrence").GetProperty("dueDate").GetString() == "2026-11-05");
        Assert.Equal(0, export.GetProperty("state").GetProperty("overallXp").GetInt32());
    }

    private static SavePlaceRequest DiscoveryRequest() => new(Guid.NewGuid(), "node/123", "Test garden", "Gardens", "c07", 35.5m, -82.5m, string.Empty, false, null, 0);

    private static async Task<SavedPlace> SaveDiscovery(HttpClient client, SavePlaceRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/discoveries/save", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SavedPlace>(Json))!;
    }
}
