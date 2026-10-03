using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Repositories;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Text.Json.Nodes;

namespace PocketQuests.Tests.Integration;

/// <summary>Restart and failure-injection coverage for schedules in real SQL.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Reopens the store between operations and deduplicates delivery under concurrent readers.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task RecurrencePauseAndStopPersistAcrossStoreRestarts()
    {
        var identity = new AccountIdentity("test", "schedule-owner");
        var now = new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero);
        var series = Guid.NewGuid();
        await using (var db = Context())
            await new SqlQuestStore(db).Read(identity, "UTC", now, default);
        await ExecuteAt(identity, new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2027-01-31", SeriesId: series, Cadence: Cadence.Months, Interval: 1, ExpectedRevision: 0), now);
        await ExecuteAt(identity, new(Guid.NewGuid(), "pause", CategoryId: "c07"), now);
        await ExecuteAt(identity, new(Guid.NewGuid(), "resume", CategoryId: "c07"), now.AddDays(1));
        var march = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);
        async Task<QuestState> Reopen()
        {
            await using var db = Context();
            return await new SqlQuestStore(db).Read(identity, "UTC", march, default);
        }

        var reads = await Task.WhenAll(Reopen(), Reopen());
        Assert.All(reads, state => Assert.Equal(2, state.Occurrences.Length));
        Assert.All(reads, state => Assert.Contains(state.Occurrences, o => o.Occurrence.DueDate == "2027-03-01"));
        var stopped = await ExecuteAt(identity, new(Guid.NewGuid(), "stop-series", SeriesId: series), march);
        Assert.Equal(QuestStatus.Frozen, stopped.Occurrences.Single(o => o.Occurrence.DueDate == "2027-03-01").Status);
        await using var finalDb = Context();
        var final = await new SqlQuestStore(finalDb).Read(identity, "UTC", march.AddMonths(2), default);
        Assert.Equal(2, final.Occurrences.Length);
        Assert.True(final.Schedule.Series.Single().Stopped);
    }

    /// <summary>Failure to write a receipt rolls back new schedule, delivery and profile changes together.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task RecurrenceReceiptFailureRollsBackScheduleAndGeneratedOccurrence()
    {
        var identity = new AccountIdentity("test", "schedule-owner");
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
        {
            await new SqlQuestStore(db).Read(identity, "UTC", now, default);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectScheduleReceipt ON Operations AFTER INSERT AS BEGIN THROW 51001, 'Injected receipt failure', 1; END;");
        }

        var command = new QuestCommand(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => ExecuteAt(identity, command, now));
            await using var db = Context();
            Assert.Equal(0, await db.Occurrences.CountAsync());
            Assert.Equal(0, await db.Operations.CountAsync());
            Assert.Equal(0, await db.Audit.CountAsync());
        }
        finally
        {
            await using var db = Context();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectScheduleReceipt;");
        }

        Assert.Single((await ExecuteAt(identity, command, now)).Occurrences);
        Assert.Single((await ExecuteAt(identity, command, now)).Occurrences);
    }

    /// <summary>Legacy idempotency receipts remain replayable as the response contract grows.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task LegacyCommandReceiptCanBeReadWithoutDefaultImmutableArrayFailure()
    {
        var identity = new AccountIdentity("test", "legacy-owner");
        var now = DateTimeOffset.UtcNow;
        await using (var db = Context())
            await new SqlQuestStore(db).Read(identity, "UTC", now, default);
        var command = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07");
        var original = await ExecuteAt(identity, command, now);
        await using (var db = Context())
        {
            var row = await db.Operations.SingleAsync();
            var result = JsonNode.Parse(row.Result)!.AsObject();
            foreach (var name in new[] { "Definitions", "Profile", "Schedule", "Penalties" })
                result.Remove(name);
            row.Result = result.ToJsonString();
            await db.SaveChangesAsync();
        }

        var replay = await ExecuteAt(identity, command, now.AddHours(1));
        Assert.Equal(original.Occurrences.Single().Occurrence.Id, replay.Occurrences.Single().Occurrence.Id);
        Assert.Empty(replay.Definitions);
        Assert.NotNull(replay.Profile);
        Assert.Empty(replay.Schedule.Series);
    }

    private async Task<QuestState> ExecuteAt(AccountIdentity identity, QuestCommand command, DateTimeOffset at)
    {
        await using var db = Context();
        return await new SqlQuestStore(db).Execute(identity, command, at, default);
    }
}
