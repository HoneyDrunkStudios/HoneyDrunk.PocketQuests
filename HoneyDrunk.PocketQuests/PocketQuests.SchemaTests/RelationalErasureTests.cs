using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.SchemaTests;

/// <summary>Account-owned erasure and actual SQL backup recovery preserve external marker evidence without shared-table overreach.</summary>
/// <param name="fixture">Only this test class's disposable database.</param>
public sealed class RelationalErasureTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private IQuestService Store => fixture.Commands();

    /// <summary>A renewed old erasing capability cannot restart expired marker retention; a first delayed erasure still removes real data.</summary>
    /// <returns>Completion after the exact retention boundary and a separately delayed first erasure.</returns>
    [Fact]
    public async Task RenewedOldErasureAfterRetentionDoesNotInventANewErasureInstant()
    {
        var owner = Identity();
        var delayed = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        await Store.Initialize(delayed, "Etc/UTC", Start);
        var erasedAt = Start.AddDays(30);
        var intent = new LifecycleIntent(owner.Subject, 2, IdentityProtocol.Erasing, erasedAt, erasedAt.AddHours(1), IdentityProtocol.ConsumerId, Capability(), Start);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", erasedAt);
        var afterRetention = erasedAt.AddDays(35);
        await fixture.Lifecycle().PruneLifecycle(afterRetention);
        var renewed = intent with { ExpiresAt = afterRetention.AddHours(1), Acknowledgment = Capability() };
        await fixture.Lifecycle().ReceiveLifecycle(renewed, "private-ack", afterRetention);
        await fixture.Lifecycle().ReceiveLifecycle(renewed with { Acknowledgment = Capability() }, "private-ack", afterRetention.AddMinutes(1));
        await using var db = fixture.Context();
        Assert.False(await db.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == owner.Subject));
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == owner.Subject));
        Assert.Equal(2, await db.Set<LifecycleMessageEntity>().CountAsync(m => m.IdentityUserId == owner.Subject));
        await fixture.Lifecycle().ReceiveLifecycle(renewed with { UserId = delayed.Subject, Acknowledgment = Capability() }, "private-ack", afterRetention);
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == delayed.Subject));
        Assert.Equal(afterRetention, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == delayed.Subject)).CreatedAt);
    }

    /// <summary>Erasure removes every owned table, retains unrelated shared rows, and cannot resurrect data after restoring an older physical backup.</summary>
    /// <returns>Completion after actual restore, externally sourced marker reapplication and exact retention-boundary checks.</returns>
    [Fact]
    public async Task PhysicalRestoreReappliesOriginalMarkerAndPurgePreservesOtherOwners()
    {
        var owner = Identity();
        var other = Identity();
        var command = await Seed(owner);
        await Store.Initialize(other, "Etc/UTC", Start);
        await Store.Execute(other, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q02"), Start);
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var otherAccount = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == other.Subject);
        var unownedAudit = Guid.NewGuid().ToString("N");
        var unownedOutbox = Guid.NewGuid();
        await fixture.Execute($$"""
            INSERT dbo.AuditRecords(Id,OccurredAt,Actor,EventName,Category,Outcome,TargetType,TargetId,TenantId,CorrelationId,Operation,ChangesJson,MetadataJson)
            SELECT TOP(1) '{{unownedAudit}}',OccurredAt,Actor,EventName,Category,Outcome,TargetType,TargetId,TenantId,CorrelationId,Operation,ChangesJson,MetadataJson
              FROM dbo.AuditRecords a JOIN pocketquests.AccountAuditRecord own ON own.AuditRecordId=a.Id WHERE own.AccountId='{{account.Id:D}}';
            INSERT outbox.OutboxMessages(Id,Type,Payload,OccurredAt,Status,RetryCount)
              VALUES('{{unownedOutbox:D}}',N'Other.Owner',N'{}','2026-01-01T12:00:00+00:00',0,0);
            """);
        var backup = await fixture.Backup();
        var erasedAt = Start.AddDays(30);
        var intent = new LifecycleIntent(owner.Subject, 2, IdentityProtocol.Erasing, erasedAt, erasedAt.AddHours(1), IdentityProtocol.ConsumerId, Capability(), Start);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", erasedAt);
        await AssertPurged(account.Id);
        var external = await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == owner.Subject);
        Assert.Equal(erasedAt, external.CreatedAt);
        var pending = Assert.Single(await db.Set<LifecycleMessageEntity>().Where(m => m.IdentityUserId == owner.Subject).ToListAsync());
        Assert.Null(pending.AccountId);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", erasedAt.AddMinutes(1));
        Assert.Equal(erasedAt, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == owner.Subject)).CreatedAt);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Execute(owner, command, erasedAt));

        // Product traffic is withheld while the administrator proves that the old backup restored its rows.
        await fixture.Restore(backup);
        Assert.True(await db.Set<AccountEntity>().AnyAsync(a => a.Id == account.Id));
        Assert.False(await db.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == owner.Subject));
        await fixture.Lifecycle().ReapplyErasure(external.Id, external.CreatedAt, erasedAt.AddDays(1));
        await AssertPurged(account.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Read(owner, erasedAt.AddDays(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Initialize(owner, "Etc/UTC", erasedAt.AddDays(1)));
        Assert.Equal(erasedAt, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == owner.Subject)).CreatedAt);
        await fixture.Lifecycle().ReapplyErasure(owner.Subject, erasedAt.AddHours(1), erasedAt.AddDays(2));
        Assert.Equal(erasedAt, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == owner.Subject)).CreatedAt);
        Assert.Equal(otherAccount.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == otherAccount.Id)).RowVersion);
        Assert.Single((await Store.Read(other, erasedAt)).Occurrences);
        var retained = await fixture.Query($"SELECT COUNT_BIG(*) AS N FROM dbo.AuditRecords WHERE Id='{unownedAudit}'; SELECT COUNT_BIG(*) AS N FROM outbox.OutboxMessages WHERE Id='{unownedOutbox:D}';");
        Assert.All(retained, table => Assert.Equal(1L, table.Rows[0][0]));
        await fixture.Lifecycle().PruneLifecycle(erasedAt.AddDays(35).AddTicks(-1));
        Assert.True(await db.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == owner.Subject));
        await fixture.Lifecycle().PruneLifecycle(erasedAt.AddDays(35));
        Assert.False(await db.Set<ErasureMarkerEntity>().AnyAsync(m => m.Id == owner.Subject));
        Assert.Equal(1L, (await fixture.Query($"SELECT COUNT_BIG(*) AS N FROM outbox.OutboxMessages WHERE Id='{unownedOutbox:D}';"))[0].Rows[0][0]);
    }

    /// <summary>Pre-account markers block initialization, old/future external evidence is rejected, and cleanup removes only linked envelopes.</summary>
    /// <returns>Completion after marker and one-hour ownership-boundary checks.</returns>
    [Fact]
    public async Task MarkerAndEnvelopeRetentionRespectOriginalInstantsAndOwnership()
    {
        var owner = Identity();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Lifecycle().ReapplyErasure(owner.Subject, Start.AddTicks(1), Start));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Lifecycle().ReapplyErasure(owner.Subject, Start.AddDays(-35), Start));
        await fixture.Lifecycle().ReapplyErasure(owner.Subject, Start, Start);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Store.Initialize(owner, "Etc/UTC", Start));
        var intent = new LifecycleIntent(owner.Subject, 1, IdentityProtocol.Erasing, Start, Start.AddHours(1), IdentityProtocol.ConsumerId, Capability(), Start);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", Start);
        await using var db = fixture.Context();
        var message = Assert.Single(await db.Set<LifecycleMessageEntity>().Where(m => m.IdentityUserId == owner.Subject).ToListAsync());
        await fixture.Lifecycle().PruneLifecycle(Start.AddHours(1).AddTicks(-1));
        Assert.True(await db.Set<LifecycleMessageEntity>().AnyAsync(m => m.Id == message.Id));
        await fixture.Lifecycle().PruneLifecycle(Start.AddHours(1));
        Assert.False(await db.Set<LifecycleMessageEntity>().AnyAsync(m => m.Id == message.Id));
        Assert.Equal(0L, (await fixture.Query($"SELECT COUNT_BIG(*) AS N FROM outbox.OutboxMessages WHERE Id='{message.OutboxMessageId:D}';"))[0].Rows[0][0]);
        var renewed = intent with { Acknowledgment = Capability(), EffectiveAt = Start.AddHours(2), ExpiresAt = Start.AddHours(3) };
        await fixture.Lifecycle().ReceiveLifecycle(renewed, "private-ack", Start.AddHours(2));
        message = Assert.Single(await db.Set<LifecycleMessageEntity>().Where(m => m.IdentityUserId == owner.Subject).ToListAsync());
        await fixture.Execute($"UPDATE outbox.OutboxMessages SET Status={(int)OutboxMessageStatus.Dispatched} WHERE Id='{message.OutboxMessageId:D}';");
        await fixture.Lifecycle().PruneLifecycle(Start.AddHours(2));
        Assert.False(await db.Set<LifecycleMessageEntity>().AnyAsync(m => m.Id == message.Id));
        Assert.Equal(Start, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == owner.Subject)).CreatedAt);
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());

    private static string Capability() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private async Task<QuestCommand> Seed(AccountIdentity owner)
    {
        await Store.Initialize(owner, "Etc/UTC", Start);
        var skill = Guid.NewGuid().ToString("D");
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSkill, SkillId: skill, SkillName: "Owned custom skill", ExpectedRevision: 0), Start);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.AssessSkill, SkillId: skill, Experience: Experience.Experienced), Start);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Interests, Interests: ["c01"]), Start);
        var quest = new Quest(Guid.NewGuid().ToString("D"), "Owned definition", "Owned criterion", "c01", Rank.F, Effort.Small, [new("a01", 10000)], [new(skill, 10000)], true);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0), Start);
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id);
        await Store.Execute(owner, accept, Start);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        await Store.Execute(owner, complete, Start);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId), Start.AddSeconds(1));
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: quest.Id, DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), Start.AddSeconds(2));
        await Store.CreateAnchor(owner, Guid.NewGuid(), Guid.NewGuid(), Start.AddSeconds(2), Start.AddSeconds(2));
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Zone, NewZone: "America/New_York", ExpectedZone: "Etc/UTC", ConfirmZoneChange: true), Start.AddSeconds(3));
        await fixture.Lifecycle().ReceiveLifecycle(new(owner.Subject, 1, IdentityProtocol.Inactive, Start.AddMinutes(1), Start.AddHours(1), IdentityProtocol.ConsumerId, Capability(), Start), "private-ack", Start.AddMinutes(1));
        return accept;
    }

    private async Task AssertPurged(Guid accountId)
    {
        foreach (var table in fixture.Contract.RootElement.GetProperty("tables").EnumerateArray())
        {
            var name = table.GetProperty("name").GetString();
            if (!table.GetProperty("columns").EnumerateArray().Any(c => c.GetProperty("name").GetString() == "AccountId"))
                continue;
            var rows = await fixture.Query($"SELECT COUNT_BIG(*) AS N FROM pocketquests.[{name}] WHERE AccountId='{accountId:D}';");
            Assert.Equal(0L, rows[0].Rows[0][0]);
        }

        await using var db = fixture.Context();
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.Id == accountId));
    }
}
