using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Data;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Domain.Commands;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Quests;
using System.Data.Common;
using DomainIdentity = PocketQuests.Domain.Models.Accounts.AccountIdentity;

namespace PocketQuests.SchemaTests.Quests;

/// <summary>Exercises the approved completion service against isolated SQL and retained source history.</summary>
/// <param name="fixture">Disposable database fixture.</param>
public sealed class CompletionServiceTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Nullable client keys use CLR GUID formatting while explicit client key spelling is preserved.</summary>
    /// <returns>Completion after profile, terms and assessment identity assertions.</returns>
    [Fact]
    public async Task CompletionRetainsGuidFallbackAndExplicitClientKeyCasing()
    {
        var (owner, _) = await Setup();
        var workflow = fixture.Commands();
        var fallback = Guid.NewGuid().ToString("D");
        var explicitKey = Guid.NewGuid().ToString("D").ToUpperInvariant();
        foreach (var skill in new[] { fallback, explicitKey })
        {
            await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skill, SkillName: "Custom " + skill, ExpectedRevision: 0), Start);
            await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.AssessSkill, SkillId: skill, Experience: PocketQuests.Domain.Models.Skills.Experience.Practiced), Start);
        }

        var quest = new PocketQuests.Domain.Models.Quests.Quest(Guid.NewGuid().ToString("D"), "Fallback terms", "Done", "c01", PocketQuests.Domain.Models.Progress.Rank.F, PocketQuests.Domain.Models.Quests.Effort.Small, [new("a01", 10000)], [new(fallback, 5000), new(explicitKey, 5000)], true);
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var occurrence = Guid.NewGuid();
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: quest.Id), Start);
        await fixture.Execute($"UPDATE pocketquests.CustomSkill SET ClientKey = NULL WHERE Id = '{Guid.Parse(fallback):D}'; UPDATE pocketquests.QuestDefinition SET ClientKey = NULL WHERE Id = '{Guid.Parse(quest.Id):D}';");
        var result = await fixture.Complete(owner, new(Guid.NewGuid(), QuestActions.Complete, occurrence), Start);
        Assert.Equal(new[] { fallback, explicitKey }, result.Profile.CustomSkills!.Select(row => row.Id));
        Assert.True(result.Profile.Assessments.ContainsKey(fallback));
        Assert.True(result.Profile.Assessments.ContainsKey(explicitKey));
        Assert.Equal(quest.Id, result.Definitions.Single().Quest.Id);
        Assert.Equal(new[] { fallback, explicitKey }, result.Occurrences.Single(row => row.Occurrence.Id == occurrence).Occurrence.Quest.Skills.Select(row => row.Id));
    }

    /// <summary>A lost commit acknowledgment is resolved by the retained receipt, without a second reward or history row.</summary>
    /// <returns>Completion after an injected post-commit failure and exact command retry.</returns>
    [Fact]
    public async Task UncertainCommitRetryResolvesReceiptWithoutRepeatingMutation()
    {
        var (owner, occurrence) = await Setup();
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(new LostCommitAcknowledgment()).Options;
        await using var db = new AppDbContext(options);
        var service = new QuestService(new AccountDataService(db), new CurrentAccount(owner), new Clock(Start));
        await Assert.ThrowsAsync<TimeoutException>(() => service.Execute(command));
        Assert.Empty(db.ChangeTracker.Entries());
        var committed = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        Assert.True(await db.CommandReceipt.AnyAsync(row => row.Id == command.OperationId));
        Assert.Equal(10, (await service.Execute(command)).OverallXp);
        Assert.Equal(committed.RowVersion, (await db.Account.SingleAsync(row => row.Id == committed.Id)).RowVersion);
        Assert.Equal(1, await db.QuestCompletion.CountAsync(row => row.Id == command.OperationId));
        Assert.Equal(1, await db.QuestCommandHistory.CountAsync(row => row.Id == command.OperationId));
    }

    /// <summary>A transient failure before commit rolls back saved rows and reloads fresh state for the next attempt.</summary>
    /// <returns>Completion after proving one durable change across two independent attempts.</returns>
    [Fact]
    public async Task PreCommitTransientFailureReloadsStateBeforeRetry()
    {
        var (owner, _) = await Setup();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        var attempts = new List<PocketQuests.Data.Entities.Accounts.AccountEntity>();
        var transactions = new List<Guid>();
        var original = await db.Account.AsNoTracking().SingleAsync(row => row.IdentityUserId == owner.Subject);
        await data.ExecuteInTransaction(async token =>
        {
            var account = await db.Account.SingleAsync(row => row.Id == original.Id, token);
            attempts.Add(account);
            transactions.Add(db.Database.CurrentTransaction!.TransactionId);
            Assert.Equal(original.HasExpiryWarnings, account.HasExpiryWarnings);
            account.HasExpiryWarnings = !account.HasExpiryWarnings;
            if (attempts.Count == 1)
            {
                await db.SaveChangesAsync(token);
                throw new TimeoutException("Injected transient failure after save, before commit.");
            }

            return true;
        });
        Assert.Equal(2, attempts.Count);
        Assert.NotSame(attempts[0], attempts[1]);
        Assert.NotEqual(transactions[0], transactions[1]);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(!original.HasExpiryWarnings, (await db.Account.AsNoTracking().SingleAsync(row => row.Id == original.Id)).HasExpiryWarnings);
    }

    /// <summary>A real command reloads state and reacquires its lock after a saved but uncommitted failure.</summary>
    /// <returns>Completion after one durable reward, receipt, history and version advance.</returns>
    [Fact]
    public async Task CompletionRetriesFreshAfterSavedRowsRollBack()
    {
        var (owner, occurrence) = await Setup();
        var fault = new FailAfterFirstSave();
        var locks = new CommandLocks();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(fault, locks).Options;
        await using var db = new AppDbContext(options);
        var before = await db.Account.AsNoTracking().SingleAsync(row => row.IdentityUserId == owner.Subject);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        var service = new QuestService(new AccountDataService(db), new CurrentAccount(owner), new Clock(Start));
        var result = await service.Execute(command);
        Assert.Equal(10, result.OverallXp);
        Assert.Equal(2, fault.Accounts.Count);
        Assert.NotSame(fault.Accounts[0], fault.Accounts[1]);
        Assert.NotEqual(fault.Transactions[0], fault.Transactions[1]);
        Assert.Equal(2, locks.Count);
        Assert.All(fault.Versions, version => Assert.Equal(before.MutationVersion + 1, version));
        Assert.Empty(db.ChangeTracker.Entries());
        await using var evidence = fixture.Context();
        Assert.Equal(before.MutationVersion + 1, await evidence.Account.Where(row => row.Id == before.Id).Select(row => row.MutationVersion).SingleAsync());
        Assert.Equal(1, await evidence.CommandReceipt.CountAsync(row => row.Id == command.OperationId));
        Assert.Equal(1, await evidence.QuestCommandHistory.CountAsync(row => row.Id == command.OperationId));
        Assert.Equal(1, await evidence.QuestCompletion.CountAsync(row => row.Id == command.OperationId));
        Assert.Equal(1, await evidence.QuestOccurrenceEvent.CountAsync(row => row.Id == command.OperationId));
        Assert.Equal(10, await evidence.XpBalance.Where(row => row.AccountId == before.Id && row.TrackCode == "Overall").Select(row => row.EarnedXp).SingleAsync());
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(result), System.Text.Json.JsonSerializer.Serialize(await service.Execute(command)));
    }

    /// <summary>Transient retries are bounded, and a canceled operation never starts another attempt.</summary>
    /// <param name="cancel">Whether the first operation cancels instead of failing transiently.</param>
    /// <returns>Completion after attempt counts and owned transaction cleanup are verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionRetriesAreBoundedAndHonorCancellation(bool cancel)
    {
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        var operation = data.ExecuteInTransaction(Attempt, cancellation.Token);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        else
            await Assert.ThrowsAsync<RetryLimitExceededException>(() => operation);
        Assert.Equal(cancel ? 1 : 3, attempts);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());

        Task<bool> Attempt(CancellationToken token)
        {
            attempts++;
            if (cancel)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }

            return Task.FromException<bool>(new TimeoutException("Injected pre-commit transient failure."));
        }
    }

    /// <summary>A failed SQL save rolls back receipt, reward and history, then permits a same-scope retry.</summary>
    /// <returns>Completion after real CHECK rejection and successful recovery.</returns>
    [Fact]
    public async Task FailedCompletionRollsBackAndClearsOnlyOwnedChanges()
    {
        var (owner, occurrence) = await Setup();
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = Service(scope.ServiceProvider, owner);
        var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var version = account.MutationVersion;
        var constraint = "CK_CompletionTest_" + command.OperationId.ToString("N");
        await fixture.Execute($"ALTER TABLE pocketquests.CommandReceipt ADD CONSTRAINT [{constraint}] CHECK (Id <> '{command.OperationId:D}');");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => service.Execute(command));
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.False(await db.CommandReceipt.AnyAsync(row => row.Id == command.OperationId));
            Assert.False(await db.QuestCompletion.AnyAsync(row => row.Id == command.OperationId));
            Assert.False(await db.QuestCommandHistory.AnyAsync(row => row.Id == command.OperationId));
            Assert.Equal(version, await db.Account.Where(row => row.Id == account.Id).Select(row => row.MutationVersion).SingleAsync());
        }
        finally
        {
            await fixture.Execute($"ALTER TABLE pocketquests.CommandReceipt DROP CONSTRAINT [{constraint}];");
        }

        Assert.Equal(10, (await service.Execute(command)).OverallXp);
        Assert.Equal(version + 1, await db.Account.Where(row => row.Id == account.Id).Select(row => row.MutationVersion).SingleAsync());
        Assert.Equal(1, await db.QuestCompletion.CountAsync(row => row.Id == command.OperationId));
    }

    /// <summary>The transaction entry guard does not discard another caller's staged work or own its transaction.</summary>
    /// <returns>Completion after both ownership guards reject.</returns>
    [Fact]
    public async Task UnrelatedPendingChangesAndNestedTransactionsRemainOwnedByCaller()
    {
        var (owner, occurrence) = await Setup();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        account.HasExpiryWarnings = !account.HasExpiryWarnings;
        var service = Service(scope.ServiceProvider, owner);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Execute(command));
        Assert.Equal(EntityState.Modified, db.Entry(account).State);
        Assert.True(db.ChangeTracker.HasChanges());
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Execute(command));
        Assert.Same(transaction, db.Database.CurrentTransaction);
        await transaction.RollbackAsync();
    }

    /// <summary>Replay is read-only even after Undo, and completion leaves unrelated profile source rows untouched.</summary>
    /// <returns>Completion after byte-identical response and rowversion assertions.</returns>
    [Fact]
    public async Task CompletionAndOldReceiptReplayPreserveUnrelatedProfileRows()
    {
        var (owner, occurrence) = await Setup();
        var workflow = fixture.Commands();
        var skill = Guid.NewGuid().ToString("D");
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skill, SkillName: "Unchanged skill", ExpectedRevision: 0), Start);
        await using var db = fixture.Context();
        var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var before = await db.CustomSkill.SingleAsync(row => row.AccountId == account.Id);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        string original;
        await using (var scope = fixture.CreateScope())
            original = System.Text.Json.JsonSerializer.Serialize(await Service(scope.ServiceProvider, owner).Execute(command));
        var after = await db.CustomSkill.SingleAsync(row => row.Id == before.Id);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.Undo, occurrence, CompletionId: command.OperationId), Start.AddMinutes(1));
        var accountBeforeReplay = await db.Account.SingleAsync(row => row.Id == account.Id);
        await using (var scope = fixture.CreateScope())
            Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(await Service(scope.ServiceProvider, owner, Start.AddYears(2)).Execute(command)));
        Assert.Equal(accountBeforeReplay.RowVersion, (await db.Account.SingleAsync(row => row.Id == account.Id)).RowVersion);
    }

    /// <summary>Direct tracked and detached edits cannot rewrite immutable receipts or their insertion instant.</summary>
    /// <param name="edit">Mutation route under test.</param>
    /// <returns>Completion after EF rejects the mutation and SQL retains original values.</returns>
    [Theory]
    [InlineData("digest")]
    [InlineData("detached")]
    [InlineData("created")]
    public async Task ImmutableReceiptMetadataProtectsDirectEntityWrites(string edit)
    {
        var (owner, occurrence) = await Setup();
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        await using var scope = fixture.CreateScope();
        await Service(scope.ServiceProvider, owner).Execute(command);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = await db.CommandReceipt.SingleAsync(row => row.Id == command.OperationId);
        var original = receipt.PayloadDigest.ToArray();
        if (edit == "digest")
            receipt.PayloadDigest[0] ^= 0xff;
        else if (edit == "created")
            receipt.CreatedAt = receipt.CreatedAt.AddTicks(1);
        else
            db.Entry(receipt).CurrentValues.SetValues(new { OutcomeJson = "{\"rewritten\":true}" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(original, (await db.CommandReceipt.SingleAsync(row => row.Id == command.OperationId)).PayloadDigest);
    }

    /// <summary>Corrupted current revision pointers and completion event kinds are rejected before another receipt is saved.</summary>
    /// <param name="corruption">Retained-source inconsistency to inject in isolated SQL.</param>
    /// <returns>Completion after fail-closed validation with unchanged mutation version.</returns>
    [Theory]
    [InlineData("definition")]
    [InlineData("series")]
    [InlineData("event")]
    public async Task RetainedSourceCorruptionFailsClosed(string corruption)
    {
        var (owner, occurrence) = await Setup();
        var workflow = fixture.Commands();
        if (corruption == "definition")
        {
            var quest = new PocketQuests.Domain.Models.Quests.Quest(Guid.NewGuid().ToString("D"), "Custom", "Done", "c01", PocketQuests.Domain.Models.Progress.Rank.F, PocketQuests.Domain.Models.Quests.Effort.Small, [new("a01", 10000)], [new("s01", 10000)], true);
            await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
            await fixture.Execute($"UPDATE pocketquests.QuestDefinition SET Revision = Revision + 1 WHERE Id = '{Guid.Parse(quest.Id):D}';");
        }
        else if (corruption == "series")
        {
            var series = Guid.NewGuid();
            await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: series, Cadence: PocketQuests.Domain.Models.Schedules.Cadence.Days, Interval: 1), Start);
            await fixture.Execute($"UPDATE pocketquests.QuestSeries SET Revision = Revision + 1 WHERE Id = '{series:D}';");
        }
        else
        {
            var completed = Guid.NewGuid();
            await using var completedScope = fixture.CreateScope();
            await Service(completedScope.ServiceProvider, owner).Execute(new(completed, QuestActions.Complete, occurrence));
            await fixture.Execute($"UPDATE pocketquests.QuestOccurrenceEvent SET EventCode = 'Accepted' WHERE Id = '{completed:D}';");
        }

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var command = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, occurrence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope.ServiceProvider, owner).Execute(command));
        Assert.False(await db.CommandReceipt.AnyAsync(row => row.Id == command.OperationId));
        Assert.Equal(before.MutationVersion, await db.Account.Where(row => row.Id == before.Id).Select(row => row.MutationVersion).SingleAsync());
    }

    private static QuestService Service(IServiceProvider services, DomainIdentity owner, DateTimeOffset? at = null) => new(
        services.GetRequiredService<IAccountDataService>(),
        new CurrentAccount(owner),
        new Clock(at ?? Start));

    private async Task<(DomainIdentity owner, Guid occurrence)> Setup()
    {
        var owner = new DomainIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var store = fixture.Commands();
        await store.Initialize(owner, "Etc/UTC", Start);
        var occurrence = Guid.NewGuid();
        await store.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: "PQ-CAT-Q01"), Start);
        return (owner, occurrence);
    }

    private sealed class CurrentAccount(DomainIdentity identity) : ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity { get; } = new(identity.Issuer, identity.Subject);
    }

    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }

    private sealed class FailAfterFirstSave : SaveChangesInterceptor
    {
        internal List<PocketQuests.Data.Entities.Accounts.AccountEntity> Accounts { get; } = [];

        internal List<Guid> Transactions { get; } = [];

        internal List<long> Versions { get; } = [];

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            var account = context.ChangeTracker.Entries<PocketQuests.Data.Entities.Accounts.AccountEntity>().Single().Entity;
            Accounts.Add(account);
            Versions.Add(account.MutationVersion);
            Transactions.Add(context.Database.CurrentTransaction!.TransactionId);
            if (Accounts.Count == 1)
                throw new TimeoutException("Injected after the real command saved every row, before commit.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommandLocks : DbCommandInterceptor
    {
        internal int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal))
                Count++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class LostCommitAcknowledgment : DbTransactionInterceptor
    {
        private bool failed;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (failed)
                return Task.CompletedTask;
            failed = true;
            return Task.FromException(new TimeoutException("Injected failure after SQL committed."));
        }
    }
}
