using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Tests.Fixtures;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Real SQL tests of the executable relational command lane and original response reconstruction.</summary>
/// <param name="fixture">Only a newly deployed disposable database.</param>
public sealed class RelationalCommandTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private TestQuestWorkflow Store => fixture.Commands();

    /// <summary>Every catalog allocation, including presentation order and empty pools, round-trips through typed source rows.</summary>
    /// <returns>Completion after comparing actual public state with the existing Domain.</returns>
    [Fact]
    public async Task CatalogAcceptanceCompletionAndReadMatchExistingDomain()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var domain = new QuestAggregate("Etc/UTC");
        var index = 0;
        foreach (var quest in Catalog.Quests)
        {
            var at = Start.AddMinutes(++index);
            var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id, DueDate: "2026-01-02", PlannedTime: "14:30");
            domain.Apply(accept, at);
            Equal(domain.Project(at), await Store.Execute(owner, accept, at));
            var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
            var before = domain.Project(at);
            domain.Apply(complete, at);
            var after = domain.Project(at);
            after = after with { CompletionOutcome = CompletionOutcome.Between(before, after, complete.OperationId, accept.OccurrenceId!.Value) };
            Equal(after, await Store.Execute(owner, complete, at));
            Equal(domain.Project(at), await Store.Read(owner, at));
        }

        await AssertProjection(owner, domain.Project(Start.AddMinutes(index)));
    }

    /// <summary>A response-loss retry after Undo, a new completion and a new acceptance returns its original state and feedback.</summary>
    /// <returns>Completion after checking compact immutable receipts and version-filtered history.</returns>
    [Fact]
    public async Task CompactReceiptsReconstructOriginalResponseAfterUndoAndLaterCommands()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        var accepted = await Store.Execute(owner, accept, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var completed = await Store.Execute(owner, complete, Start.AddMinutes(1));
        var undo = new QuestCommand(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId);
        var undone = await Store.Execute(owner, undo, Start.AddMinutes(2));
        var again = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        await Store.Execute(owner, again, Start.AddMinutes(3));
        await Store.Execute(owner, Accept(), Start.AddMinutes(4));

        // New service objects simulate an ordinary application process restart; all proof/history is SQL-backed.
        Equal(accepted, await Store.Execute(owner, accept, Start.AddYears(2)));
        Equal(completed, await Store.Execute(owner, complete, Start.AddYears(2)));
        Equal(undone, await Store.Execute(owner, undo, Start.AddYears(2)));
        await Assert.ThrowsAsync<QuestConflictException>(() => Store.Execute(owner, complete with { PlannedTime = "12:30" }, Start.AddYears(2)));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var receipts = await db.Set<CommandReceiptEntity>().Where(r => r.AccountId == account.Id).ToListAsync();
        Assert.Equal(5, receipts.Count);
        Assert.All(receipts, r =>
        {
            Assert.InRange(System.Text.Encoding.Unicode.GetByteCount(r.OutcomeJson), 1, 4096);
            var fields = JsonDocument.Parse(r.OutcomeJson).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "ProjectionAt", "CompletionOutcome" }, fields);
        });
        Assert.Equal(5, account.MutationVersion);
        var first = await db.Set<QuestCompletionEntity>().SingleAsync(c => c.Id == complete.OperationId);
        Assert.Equal(Start.AddMinutes(2), first.UndoneAt);
        Assert.Equal(undo.OperationId, first.UndoQuestOccurrenceEventId);
    }

    /// <summary>Late delivery replays by effective time, retains a fractional proof exactly, and never ages out ordinary pending actions.</summary>
    /// <returns>Completion after comparing online chronological and delayed delivery results.</returns>
    [Fact]
    public async Task ArbitrarilyLateFractionalProofRebuildsChronologicalStreaksAndSurvivesRestart()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var older = Accept();
        var newer = Accept();
        await Store.Execute(owner, older, Start);
        await Store.Execute(owner, newer, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var current = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, newer.OccurrenceId);
        await Store.Execute(owner, current, Start.AddDays(1));
        const double elapsed = 1234.5678;
        var proof = new RecordedActionTime(anchor.Id, anchor.BootId, 1, elapsed, Start.AddMilliseconds(elapsed));
        var delayed = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, older.OccurrenceId, RecordedTime: proof);
        var result = await Store.Execute(owner, delayed, Start.AddDays(400));
        var domain = new QuestAggregate("Etc/UTC");
        domain.Apply(older, Start);
        domain.Apply(newer, Start);
        domain.Apply(delayed, Start.AddMilliseconds(elapsed));
        domain.Apply(current, Start.AddDays(1));
        Equal(domain.Project(Start.AddDays(400)), result with { CompletionOutcome = null });
        Assert.Equal(20, result.Categories.Single(c => c.Id == "c01").Xp);
        await using var db = fixture.Context();
        var savedAnchor = await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id);
        Assert.Equal(elapsed, savedAnchor.LastElapsedMilliseconds);
        Assert.Equal(1, savedAnchor.LastOrdinal);
        var row = await db.Set<QuestCompletionEntity>().SingleAsync(c => c.Id == delayed.OperationId);
        Assert.Equal(Start.AddMilliseconds(elapsed), row.RecordedAt);
        Equal(result, await Store.Execute(owner, delayed, Start.AddDays(800)));
        await AssertProjection(owner, result);
    }

    /// <summary>Temporary clock lead is retryable; permanent mismatch and rejected Undo leave no receipt or ordinal.</summary>
    /// <returns>Completion after exact-payload retries and boundary assertions.</returns>
    [Fact]
    public async Task ClockFailuresDoNotConsumeProofAndFixedFloorDoesNotAccumulate()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId, RecordedTime: new(anchor.Id, anchor.BootId, 1, 10_000.25, Start.AddMilliseconds(10_000.25)));
        await Assert.ThrowsAsync<SyncClockNotReadyException>(() => Store.Execute(owner, command, Start));
        await AssertProofUnchanged(owner, anchor.Id, command.OperationId);
        var mismatch = command with { RecordedTime = command.RecordedTime! with { DeviceUtc = Start.AddHours(1) } };
        await Assert.ThrowsAsync<QuestValidationException>(() => Store.Execute(owner, mismatch, Start.AddMinutes(1)));
        await AssertProofUnchanged(owner, anchor.Id, command.OperationId);
        var result = await Store.Execute(owner, command, Start.AddSeconds(6));
        Equal(result, await Store.Execute(owner, command, Start.AddSeconds(-30)));
        await Assert.ThrowsAsync<SyncClockNotReadyException>(() => Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start));
        var refreshed = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start.AddSeconds(6), Start.AddSeconds(6));
        Assert.Equal(Start.AddMilliseconds(10_000.25), refreshed.RecordedTimeFloor);
        var undo = new QuestCommand(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: command.OperationId, RecordedTime: new(refreshed.Id, refreshed.BootId, 1, 0.125, Start.AddSeconds(6).AddMilliseconds(0.125)));
        var undone = await Store.Execute(owner, undo, Start.AddSeconds(6));
        Assert.Equal(0, undone.OverallXp);
        await using var db = fixture.Context();
        Assert.Equal(Start.AddMilliseconds(10_000.25), (await db.Set<QuestCompletionEntity>().SingleAsync(c => c.Id == command.OperationId)).UndoneAt);
    }

    /// <summary>Only occurrences visible to a proof or accepted under it can be completed with that proof.</summary>
    /// <returns>Completion after rejecting cross-account, wrong-boot and unseen-occurrence proofs.</returns>
    [Fact]
    public async Task IdentityOwnershipAndAnchorVisibilityAreEnforced()
    {
        var owner = Identity();
        var other = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        await Store.Initialize(other, "Etc/UTC", Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId, RecordedTime: new(anchor.Id, anchor.BootId, 1, 1000, Start.AddSeconds(1)));
        await Assert.ThrowsAsync<QuestValidationException>(() => Store.Execute(owner, complete, Start.AddSeconds(2)));
        await Assert.ThrowsAsync<QuestValidationException>(() => Store.Execute(other, complete, Start.AddSeconds(2)));
        await Assert.ThrowsAsync<QuestNotFoundException>(() => Store.Execute(other, complete with { RecordedTime = null }, Start.AddSeconds(2)));
        var offlineAccept = Accept() with { RecordedTime = new(anchor.Id, anchor.BootId, 1, 2000, Start.AddSeconds(2)) };
        await Store.Execute(owner, offlineAccept, Start.AddDays(90));
        var offlineComplete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, offlineAccept.OccurrenceId, RecordedTime: new(anchor.Id, anchor.BootId, 2, 3000, Start.AddSeconds(3)));
        await Assert.ThrowsAsync<QuestValidationException>(() => Store.Execute(owner, offlineComplete with { RecordedTime = offlineComplete.RecordedTime! with { BootId = Guid.NewGuid() } }, Start.AddDays(90)));
        await Store.Execute(owner, offlineComplete, Start.AddDays(90));

        // Provider/issuer representation changes do not fork a canonical Identity account.
        Equal(await Store.Read(owner, Start.AddDays(90)), await Store.Read(owner with { Issuer = "another-validated-provider" }, Start.AddDays(90)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Initialize(new("issuer", "provider-subject"), "Etc/UTC", Start));
    }

    /// <summary>Concurrent commands serialize; one completion earns XP, both receipts are stable, and read calls do not rewrite rows.</summary>
    /// <returns>Completion after two independent connections and timestamp/rowversion checks.</returns>
    [Fact]
    public async Task ConcurrentCompletionAndReadOnlyProjectionPreserveSource()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var first = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var second = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var results = await Task.WhenAll(Store.Execute(owner, first, Start.AddMinutes(1)), Store.Execute(owner, second, Start.AddMinutes(1)));
        Assert.Single(results, s => s.CompletionOutcome is not null);
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var occurrence = await db.Set<QuestOccurrenceEntity>().SingleAsync(o => o.Id == accept.OccurrenceId);
        var ledger = await db.Set<XpLedgerEntryEntity>().Where(e => e.AccountId == account.Id).OrderBy(e => e.Id).ToListAsync();
        await Store.Read(owner, Start.AddYears(1));
        Assert.Equal(account.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == account.Id)).RowVersion);
        Assert.Equal(occurrence.RowVersion, (await db.Set<QuestOccurrenceEntity>().SingleAsync(o => o.Id == occurrence.Id)).RowVersion);
        Assert.Equal(JsonSerializer.Serialize(ledger), JsonSerializer.Serialize(await db.Set<XpLedgerEntryEntity>().Where(e => e.AccountId == account.Id).OrderBy(e => e.Id).ToListAsync()));
        Assert.Single(await db.Set<QuestCompletionEntity>().Where(c => c.AccountId == account.Id).ToListAsync());
        Equal(results[0], await Store.Execute(owner, first, Start.AddYears(1)));
        Equal(results[1], await Store.Execute(owner, second, Start.AddYears(1)));
    }

    /// <summary>The exact 24-hour Undo boundary is rejected without a receipt; a recorded in-window Undo can arrive much later.</summary>
    /// <returns>Completion after effective-time and projection assertions.</returns>
    [Fact]
    public async Task UndoBoundaryAndLateUndoRecomputeWithoutChangingPriorReceipt()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var original = await Store.Execute(owner, complete, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var undo = new QuestCommand(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId);
        await Assert.ThrowsAsync<QuestConflictException>(() => Store.Execute(owner, undo, Start.AddHours(24)));
        var delayed = undo with { RecordedTime = new(anchor.Id, anchor.BootId, 1, 86_399_999.5, Start.AddMilliseconds(86_399_999.5)) };
        var result = await Store.Execute(owner, delayed, Start.AddDays(365));
        Assert.Equal(0, result.OverallXp);
        Assert.Empty(result.Ledger);
        Equal(original, await Store.Execute(owner, complete, Start.AddDays(366)));
        await AssertProjection(owner, result);
    }

    /// <summary>An Audit insert failure rolls back the receipt, source event, proof cursor and projections together.</summary>
    /// <returns>Completion after injecting a test-only shared-table constraint and retrying unchanged.</returns>
    [Fact]
    public async Task SharedAuditFailureRollsBackEntireCommandAndExactRetrySucceeds()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        var anchor = await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId, RecordedTime: new(anchor.Id, anchor.BootId, 1, 1000, Start.AddSeconds(1)));
        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);

        // Synthetic scratch DB only. This is fault injection, not a shared Audit schema change.
        await fixture.Execute($"ALTER TABLE dbo.AuditRecords ADD CONSTRAINT CK_Test_AuditFailure CHECK (Actor <> N'{owner.Subject}' OR EventName <> N'pocketquests.quest.complete');");
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => Store.Execute(owner, complete, Start.AddSeconds(2)));
            Assert.Equal(547, Assert.IsType<SqlException>(failure.InnerException).Number);
            await AssertProofUnchanged(owner, anchor.Id, complete.OperationId);
            Assert.False(await db.Set<QuestCompletionEntity>().AnyAsync(c => c.Id == complete.OperationId));
            Assert.False(await db.Set<QuestOccurrenceEventEntity>().AnyAsync(e => e.Id == complete.OperationId));
            Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
            Assert.Equal(0, (await Store.Read(owner, Start.AddSeconds(2))).OverallXp);
        }
        finally
        {
            await fixture.Execute("ALTER TABLE dbo.AuditRecords DROP CONSTRAINT CK_Test_AuditFailure;");
        }

        Assert.Equal(10, (await Store.Execute(owner, complete, Start.AddSeconds(2))).OverallXp);
        var audit = (await fixture.Query($"SELECT a.Actor,a.EventName,a.MetadataJson FROM dbo.AuditRecords a JOIN pocketquests.AccountAuditRecord o ON o.AuditRecordId=a.Id WHERE o.AccountId='{before.Id:D}' ORDER BY a.EventName;"))[0];
        Assert.Equal(2, audit.Rows.Count);
        Assert.Equal(owner.Subject, audit.Rows[1]["Actor"]);
        Assert.Contains(complete.OperationId.ToString("N"), (string)audit.Rows[1]["MetadataJson"], StringComparison.Ordinal);
    }

    /// <summary>The EF service role permits owned application writes but cannot rewrite immutable history, catalogs, or lifecycle fences.</summary>
    /// <returns>Completion after actual SQL impersonation, without provisioning a login.</returns>
    [Fact]
    public async Task RuntimeRoleAllowsEfWritesAndProtectsCatalogsHistoryAndLifecycle()
    {
        await fixture.Script("runtime-role-probes.sql");
    }

    /// <summary>Erasure markers and inactive lifecycle state fence account access including old receipt replay.</summary>
    /// <returns>Completion after verifying pre-account and existing-account barriers.</returns>
    [Fact]
    public async Task CanonicalLifecycleFenceAlsoProtectsReceiptReplay()
    {
        var erased = Identity();
        await fixture.Execute($"INSERT pocketquests.ErasureMarker(Id,CreatedAt) VALUES('{erased.Subject}','2026-01-01T00:00:00+00:00');");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Initialize(erased, "Etc/UTC", Start));
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        await Store.Execute(owner, accept, Start);
        await fixture.Execute($"INSERT pocketquests.AccountLifecycleState(Id,IdentityUserId,AccountId,Version,StateCode,EffectiveAt) SELECT NEWID(),IdentityUserId,Id,1,'Inactive','2026-01-01T13:00:00+00:00' FROM pocketquests.Account WHERE IdentityUserId='{owner.Subject}';");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Read(owner, Start.AddHours(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Execute(owner, accept, Start.AddHours(1)));
        await using var db = fixture.Context();
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == erased.Subject));
    }

    /// <summary>Receipt size stays constant as account history grows, and simultaneous identical retries do not duplicate history/audit.</summary>
    /// <returns>Completion after creating synthetic account history and racing two connections.</returns>
    [Fact]
    public async Task ReceiptSizeIsIndependentOfAccountSizeAndSamePayloadRaceIsIdempotent()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept();
        var first = await Task.WhenAll(Store.Execute(owner, accept, Start), Store.Execute(owner, accept, Start));
        Equal(first[0], first[1]);
        QuestState? last = null;
        for (var i = 0; i < 48; i++)
            last = await Store.Execute(owner, Accept(), Start);
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var receipts = await db.Set<CommandReceiptEntity>().Where(r => r.AccountId == account.Id).ToListAsync();
        Assert.Equal(49, receipts.Count);
        Assert.Single(receipts.Select(r => r.OutcomeJson).Distinct());
        Assert.True(JsonSerializer.Serialize(last).Length > receipts[0].OutcomeJson.Length * 100);
        Assert.Equal(49, await db.Set<AccountAuditRecordEntity>().CountAsync(a => a.AccountId == account.Id));
        Equal(first[0], await Store.Execute(owner, accept, Start.AddYears(1)));
    }

    /// <summary>A new OS process reconstructs the identical original response, independent of randomized string hashing.</summary>
    /// <returns>Completion after a child process uses only SQL history and compact receipts.</returns>
    [Fact]
    public async Task FreshProcessReplaysOriginalResponseAfterLaterUndo()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        var accept = Accept() with { QuestId = "PQ-CAT-Q03" };
        await Store.Execute(owner, accept, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        var original = await Store.Execute(owner, complete, Start.AddMinutes(1));
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId), Start.AddMinutes(2));
        var input = Path.Combine(Path.GetTempPath(), "pq-replay-probe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(complete));
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ReplayProbe", "PocketQuests.ReplayProbe.dll"));
            info.ArgumentList.Add(input);
            info.ArgumentList.Add(owner.Subject);
            info.ArgumentList.Add(Start.AddYears(1).ToString("O", CultureInfo.InvariantCulture));
            info.Environment["POCKETQUESTS_REPLAY_PROBE_CONNECTION"] = fixture.Connection;
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the replay probe.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                throw;
            }

            Assert.Equal(0, process.ExitCode);
            Assert.Equal(string.Empty, await error);
            Assert.Equal(JsonSerializer.Serialize(original), (await output).Trim());
        }
        finally
        {
            File.Delete(input);
        }
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static QuestCommand Accept() => new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01");

    private static void Equal(QuestState expected, QuestState actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private async Task AssertProofUnchanged(AccountIdentity owner, Guid anchorId, Guid operationId)
    {
        await using var db = fixture.Context();
        var anchor = await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchorId);
        Assert.Equal(0, anchor.LastOrdinal);
        Assert.Equal(0, anchor.LastElapsedMilliseconds);
        Assert.False(await db.Set<CommandReceiptEntity>().AnyAsync(r => r.Id == operationId));
        Assert.Equal(owner.Subject, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == anchor.AccountId)).IdentityUserId);
    }

    private async Task AssertProjection(AccountIdentity owner, QuestState state)
    {
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        Assert.Equal(account.MutationVersion, account.ProjectionVersion);
        var balances = await db.Set<XpBalanceEntity>().Where(b => b.AccountId == account.Id).ToListAsync();
        Assert.All(balances, b => Assert.Equal(account.ProjectionVersion, b.ProjectionVersion));
        Assert.Equal(state.OverallXp, balances.Single(b => b.TrackCode == "Overall").EarnedXp);
        foreach (var value in state.Categories)
            Assert.Equal(value.Xp, balances.Single(b => b.CategoryId == value.Id).EarnedXp);
        foreach (var value in state.Attributes)
            Assert.Equal(value.Xp, balances.Single(b => b.AttributeId == value.Id).EarnedXp);
        foreach (var value in state.Skills)
            Assert.Equal(value.Xp, balances.Single(b => b.SystemSkillId == value.Id).EarnedXp);
        var ledger = await db.Set<XpLedgerEntryEntity>().Where(b => b.AccountId == account.Id).ToListAsync();
        Assert.Equal(state.Ledger.Sum(e => e.Amount), ledger.Sum(e => e.Amount));
        var rewards = await db.Set<AccountEntitlementEntity>().Where(b => b.AccountId == account.Id).ToListAsync();
        Assert.All(state.Entitlements, expected => Assert.Equal(expected.Earned, rewards.Single(r => r.ProfileRewardId == expected.Id).IsEarned));
    }
}
