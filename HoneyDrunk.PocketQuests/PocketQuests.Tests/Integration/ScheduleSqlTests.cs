using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;

namespace PocketQuests.Tests.Integration;

/// <summary>Restart and failure-injection coverage for schedules in real SQL.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Reopens the store between operations and deduplicates delivery under concurrent readers.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task RecurrencePauseAndStopPersistAcrossStoreRestarts()
    {
        var identity = TestIdentity("schedule-owner");
        var now = new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero);
        var series = Guid.NewGuid();
        await using (var db = Context())
            await Store().Initialize(identity, "UTC", now, default);
        await ExecuteAt(identity, new(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2027-01-31", SeriesId: series, Cadence: Cadence.Months, Interval: 1, ExpectedRevision: 0), now);
        await ExecuteAt(identity, new(Guid.NewGuid(), "pause", CategoryId: "c07"), now);
        await ExecuteAt(identity, new(Guid.NewGuid(), "resume", CategoryId: "c07"), now.AddDays(1));
        var march = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);
        async Task<QuestState> Reopen()
        {
            await using var db = Context();
            return await Store().Read(identity, march, default);
        }

        await Commands().Reconcile(identity, march);
        var reads = await Task.WhenAll(Reopen(), Reopen());
        Assert.All(reads, state => Assert.Equal(2, state.Occurrences.Length));
        Assert.All(reads, state => Assert.Contains(state.Occurrences, o => o.Occurrence.DueDate == "2027-03-01"));
        var stopped = await ExecuteAt(identity, new(Guid.NewGuid(), "stop-series", SeriesId: series), march);
        Assert.Equal(QuestStatus.Frozen, stopped.Occurrences.Single(o => o.Occurrence.DueDate == "2027-03-01").Status);
        await using var finalDb = Context();
        var final = await Store().Read(identity, march.AddMonths(2), default);
        Assert.Equal(2, final.Occurrences.Length);
        Assert.True(final.Schedule.Series.Single().Stopped);
    }

    /// <summary>Failure to write a receipt rolls back new schedule, delivery and profile changes together.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task RecurrenceReceiptFailureRollsBackScheduleAndGeneratedOccurrence()
    {
        var identity = TestIdentity("schedule-owner");
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        await using (var db = Context())
        {
            await Store().Initialize(identity, "UTC", now, default);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectScheduleReceipt ON pocketquests.CommandReceipt AFTER INSERT AS BEGIN THROW 51001, 'Injected receipt failure', 1; END;");
        }

        var command = new QuestCommand(Guid.NewGuid(), "save-series", QuestId: "PQ-CAT-Q07", DueDate: "2026-09-28", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => ExecuteAt(identity, command, now));
            Assert.Equal(51001, Assert.IsType<Microsoft.Data.SqlClient.SqlException>(failure.InnerException).Number);
            await using var db = Context();
            Assert.Equal(0, await db.Read.Set<QuestOccurrenceEntity>().CountAsync());
            Assert.Equal(0, await db.Read.Set<CommandReceiptEntity>().CountAsync());
            Assert.Equal(0, await db.Audit.CountAsync());
        }
        finally
        {
            await using var db = Context();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER pocketquests.RejectScheduleReceipt;");
        }

        Assert.Single((await ExecuteAt(identity, command, now)).Occurrences);
        Assert.Single((await ExecuteAt(identity, command, now)).Occurrences);
    }

    /// <summary>Compact receipts replay the original response after subsequent commands without storing account state.</summary>
    /// <returns>The completed SQL regression.</returns>
    [Fact]
    public async Task CompactReceiptPreservesOriginalResponseAfterFurtherChanges()
    {
        var identity = TestIdentity("receipt-owner");
        var now = DateTimeOffset.UtcNow;
        await Store().Initialize(identity, "UTC", now, default);
        var command = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07");
        var original = await ExecuteAt(identity, command, now);
        await ExecuteAt(identity, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01"), now.AddMinutes(1));
        await using var db = Context();
        var receipt = await db.Read.Set<CommandReceiptEntity>().SingleAsync(r => r.Id == command.OperationId);
        Assert.DoesNotContain("Occurrences", receipt.OutcomeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Definitions", receipt.OutcomeJson, StringComparison.OrdinalIgnoreCase);
        var replay = await ExecuteAt(identity, command, now.AddHours(1));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(replay));
    }

    private async Task<QuestState> ExecuteAt(AccountIdentity identity, QuestCommand command, DateTimeOffset at)
    {
        await using var db = Context();
        return await Store().Execute(identity, command, at, default);
    }
}
