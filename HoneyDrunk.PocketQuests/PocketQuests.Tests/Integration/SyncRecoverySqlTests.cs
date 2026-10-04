using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Verifies recoverable clock failures and permanent proof failures through the HTTP contract and SQL.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Temporary clock lead retains an immutable command for retry, including Undo and receipt replay.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task ClockNotReady_Returns503AndLaterAcceptsUnchangedUndo()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var clock = new RecoveryClock(now);
        using var host = new Host(Connection, clock: clock);
        using var client = host.Client("sync-recovery");
        await Setup(client);
        var anchorResponse = await client.PostAsJsonAsync("/api/sync-anchor", new { DeviceId = Guid.NewGuid(), BootId = Guid.NewGuid(), DeviceUtc = now });
        anchorResponse.EnsureSuccessStatusCode();
        var anchor = (await anchorResponse.Content.ReadFromJsonAsync<SyncAnchor>(Json))!;
        var occurrence = Guid.NewGuid();
        RecordedActionTime Proof(int ordinal, int elapsed) => new(anchor.Id, anchor.BootId, ordinal, elapsed, now.AddMilliseconds(elapsed));
        var accept = new QuestCommand(Guid.NewGuid(), "accept", occurrence, QuestId: "PQ-CAT-Q07", RecordedTime: Proof(1, 6000));

        // New-process and wall-clock mismatch proofs stay permanent 400s; waiting cannot repair them.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", accept with { RecordedTime = Proof(1, 100) with { BootId = Guid.NewGuid() } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", accept with { RecordedTime = Proof(1, 100) with { DeviceUtc = now.AddHours(1) } })).StatusCode);
        var tooEarly = await client.PostAsJsonAsync("/api/commands", accept);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, tooEarly.StatusCode);
        Assert.Contains("retry", await tooEarly.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using (var db = Context())
        {
            Assert.Empty(await db.Read.Set<CommandReceiptEntity>().ToListAsync());
            Assert.Equal(0, (await db.Read.Set<SyncAnchorEntity>().SingleAsync()).LastOrdinal);
        }

        clock.Now = now.AddSeconds(2);
        await Command(client, accept);
        var complete = new QuestCommand(Guid.NewGuid(), "complete", occurrence, RecordedTime: Proof(2, 6100));
        Assert.Equal(10, (await Command(client, complete)).OverallXp);
        var undo = new QuestCommand(Guid.NewGuid(), "undo", occurrence, CompletionId: complete.OperationId, RecordedTime: Proof(3, 6200));
        var saved = JsonSerializer.Serialize(undo);
        clock.Now = now;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/commands", undo)).StatusCode);
        await using (var db = Context())
        {
            Assert.Empty(await db.Read.Set<QuestOccurrenceEventEntity>().Where(e => e.EventCode == "Undone").ToListAsync());
            Assert.Equal(2, (await db.Read.Set<SyncAnchorEntity>().SingleAsync()).LastOrdinal);
        }

        clock.Now = now.AddSeconds(3);
        var restored = JsonSerializer.Deserialize<QuestCommand>(saved)!;
        Assert.Equal(0, (await Command(client, restored)).OverallXp);
        Assert.Equal(0, (await Command(client, restored)).OverallXp);
        Assert.Equal(saved, JsonSerializer.Serialize(restored));
        await using var final = Context();
        Assert.Single(await final.Read.Set<QuestOccurrenceEventEntity>().Where(e => e.EventCode == "Undone").ToListAsync());
        Assert.Equal(3, await final.Read.Set<CommandReceiptEntity>().CountAsync());
    }

    /// <summary>A permanent rejection leaves the anchor ordinal available for independent queued Undo and acceptance.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task RejectedCommand_DoesNotPreventLaterIndependentUndoAndAcceptance()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var clock = new RecoveryClock(now);
        using var host = new Host(Connection, clock: clock);
        using var client = host.Client("sync-recovery");
        await Setup(client);
        var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07"));
        var occurrence = accepted.Occurrences.Single().Occurrence.Id;
        var complete = new QuestCommand(Guid.NewGuid(), "complete", occurrence);
        await Command(client, complete);
        var anchorResponse = await client.PostAsJsonAsync("/api/sync-anchor", new { DeviceId = Guid.NewGuid(), BootId = Guid.NewGuid(), DeviceUtc = now });
        anchorResponse.EnsureSuccessStatusCode();
        var anchor = (await anchorResponse.Content.ReadFromJsonAsync<SyncAnchor>(Json))!;
        RecordedActionTime Proof(int ordinal) => new(anchor.Id, anchor.BootId, ordinal, ordinal * 100, now.AddMilliseconds(ordinal * 100));
        var rejected = new QuestCommand(Guid.NewGuid(), "accept", Guid.NewGuid(), QuestId: "missing-quest", RecordedTime: Proof(1));
        var undo = new QuestCommand(Guid.NewGuid(), "undo", occurrence, CompletionId: complete.OperationId, RecordedTime: Proof(2));
        var next = new QuestCommand(Guid.NewGuid(), "accept", Guid.NewGuid(), QuestId: "PQ-CAT-Q07", RecordedTime: Proof(3));
        var savedUndo = JsonSerializer.Serialize(undo);
        clock.Now = now.AddSeconds(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", rejected)).StatusCode);
        Assert.Equal(0, (await Command(client, undo)).OverallXp);
        Assert.Equal(2, (await Command(client, next)).Occurrences.Length);
        Assert.Equal(0, (await Command(client, JsonSerializer.Deserialize<QuestCommand>(savedUndo)!)).OverallXp);
        Assert.Equal(savedUndo, JsonSerializer.Serialize(undo));
        await using var final = Context();
        Assert.Equal(3, (await final.Read.Set<SyncAnchorEntity>().SingleAsync()).LastOrdinal);
        Assert.Single(await final.Read.Set<QuestOccurrenceEventEntity>().Where(e => e.EventCode == "Undone").ToListAsync());
        Assert.Equal(4, await final.Read.Set<CommandReceiptEntity>().CountAsync());
        Assert.False(await final.Read.Set<CommandReceiptEntity>().AnyAsync(operation => operation.Id == rejected.OperationId));
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
