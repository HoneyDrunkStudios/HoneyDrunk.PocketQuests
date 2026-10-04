using Microsoft.EntityFrameworkCore;
using PocketQuests.Data;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;
using PocketQuests.Domain.Services.Accounts;
using PocketQuests.Domain.Services.Categories;
using PocketQuests.Domain.Services.Progress;
using PocketQuests.Domain.Services.Synchronization;
using System.Data;
using System.Text.Json;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Applies existing quest rules through entity business services in explicit EF transactions.</summary>
/// <param name="db">Scoped QuestDbContext dependency.</param>
/// <param name="accountData">Scoped IAccountDataService dependency.</param>
/// <param name="lifecycleData">Scoped IAccountLifecycleStateDataService dependency.</param>
/// <param name="markerData">Scoped IErasureMarkerDataService dependency.</param>
/// <param name="receiptData">Scoped ICommandReceiptDataService dependency.</param>
/// <param name="anchorData">Scoped ISyncAnchorDataService dependency.</param>
/// <param name="accountService">Scoped IAccountService dependency.</param>
/// <param name="receiptService">Scoped ICommandReceiptService dependency.</param>
/// <param name="anchorService">Scoped ISyncAnchorService dependency.</param>
/// <param name="definitionService">Scoped IQuestDefinitionService dependency.</param>
/// <param name="seriesService">Scoped IQuestSeriesService dependency.</param>
/// <param name="occurrenceService">Scoped IQuestOccurrenceService dependency.</param>
/// <param name="completionService">Scoped IQuestCompletionService dependency.</param>
/// <param name="ledgerService">Scoped IXpLedgerEntryService dependency.</param>
/// <param name="balanceService">Scoped IXpBalanceService dependency.</param>
/// <param name="categoryService">Scoped ICategoryProgressService dependency.</param>
/// <param name="entitlementService">Scoped IAccountEntitlementService dependency.</param>
/// <param name="historyService">Scoped IQuestCommandHistoryService dependency.</param>
/// <param name="auditService">Scoped IAccountAuditRecordService dependency.</param>
public sealed partial class QuestService(QuestDbContext db, IAccountDataService accountData, IAccountLifecycleStateDataService lifecycleData, IErasureMarkerDataService markerData, ICommandReceiptDataService receiptData, ISyncAnchorDataService anchorData, IAccountService accountService, ICommandReceiptService receiptService, ISyncAnchorService anchorService, IQuestDefinitionService definitionService, IQuestSeriesService seriesService, IQuestOccurrenceService occurrenceService, IQuestCompletionService completionService, IXpLedgerEntryService ledgerService, IXpBalanceService balanceService, ICategoryProgressService categoryService, IAccountEntitlementService entitlementService, IQuestCommandHistoryService historyService, IAccountAuditRecordService auditService) : IQuestService
{
    private const int RequestReconciliationLimit = 100;

    /// <inheritdoc />
    public async Task Initialize(AccountIdentity identity, string zone, DateTimeOffset now, CancellationToken token = default)
    {
        var canonicalZone = Scheduling.Zone(zone).Id;
        now = now.ToUniversalTime();
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await RequireAccess(identity, true, token);
        var account = await accountData.GetByIdentityUserIdAsync(identity.Subject, token);
        if (account is null)
        {
            account = await accountService.SaveAsync(
                new AccountEntity
                {
                    Id = Guid.NewGuid(),
                    IdentityUserId = identity.Subject,
                    TimeZoneId = canonicalZone,
                    LastRecordedAt = now,
                    ProjectionAsOfAt = now,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
            var barrier = await lifecycleData.GetByIdentityUserIdAsync(identity.Subject, token);
            if (barrier is not null)
                barrier.AccountId = account.Id;
            await db.SaveChangesAsync(token);
        }

        await transaction.CommitAsync(token);
    }

    /// <summary>Projects source history without writing account rows or materialized balances.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Current source-derived state.</returns>
    public async Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await RequireAccess(identity, false, token);
        var account = await Account(identity, token);
        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var projectionAt = Max(now, account.LastRecordedAt);
        if (aggregate.Reconcile(projectionAt, RequestReconciliationLimit).HasMore)
            throw new ReconciliationPendingException("Recurring deliveries are being reconciled. Retry after maintenance advances the retained backlog.");
        var result = aggregate.Project(projectionAt);
        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Executes a supported command or reconstructs its exact original response from compact receipt/history.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="command">Immutable client command.</param>
    /// <param name="now">Server receipt clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The original command state, including its original completion feedback.</returns>
    public async Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token = default)
    {
        var digest = CommandDigest.Compute(command);
        now = now.ToUniversalTime();
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await RequireAccess(identity, true, token);
        var account = await Account(identity, token);
        var receipt = await receiptData.FindByIdAsync(command.OperationId, token);
        if (receipt is not null)
        {
            if (receipt.AccountId != account.Id)
                throw new QuestConflictException("Operation ID already belongs to another account.");
            if (receipt.ApiVersion != 1 || receipt.DigestVersion != 1 || receipt.OutcomeVersion != 2)
                throw new NotSupportedException("The stored receipt version requires its original replay implementation.");
            if (!receipt.PayloadDigest.AsSpan().SequenceEqual(digest) || receipt.CommandType != command.Action)
                throw new QuestConflictException("Operation ID already belongs to a different payload.");
            var original = JsonSerializer.Deserialize<CompactOutcome>(receipt.OutcomeJson) ?? throw new InvalidOperationException("Receipt is invalid.");
            var originalReplay = await historyService.ReplayAsync(account, receipt.AppliedMutationVersion, token);
            var replay = originalReplay.Aggregate.Project(original.ProjectionAt) with { CompletionOutcome = originalReplay.Outcome };
            await transaction.CommitAsync(token);
            return replay;
        }

        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var logicalNow = Max(now, account.LastRecordedAt);
        var reconciliation = aggregate.Reconcile(logicalNow, RequestReconciliationLimit);
        if (reconciliation.HasMore)
        {
            await PersistReconciliation(identity, account, aggregate, logicalNow, now, RequestReconciliationLimit, hasMore: true, token);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            throw new ReconciliationPendingException("A bounded recurrence batch committed. Retry this exact command; its proof and receipt were not consumed.");
        }

        var recorded = await RecordedAt(account, command, aggregate, now, logicalNow, token);
        var projectionAt = Max(logicalNow, recorded);
        var before = aggregate.Project(projectionAt);
        var actionBudget = RequestReconciliationLimit - reconciliation.Processed;
        var actionReconciliation = aggregate.Apply(command, recorded, actionBudget);
        var result = aggregate.Project(projectionAt);
        if (command.Action == QuestActions.Complete)
            result = result with { CompletionOutcome = CompletionOutcome.Between(before, result, command.OperationId, command.OccurrenceId!.Value) };
        var hasPending = actionReconciliation.HasMore || aggregate.Reconcile(projectionAt, 0).HasMore;
        await CommitMutation(identity, account, command, digest, aggregate, result, recorded, logicalNow, projectionAt, now, token, RequestReconciliationLimit, actionBudget, hasPending: hasPending);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Issues a durable fixed-floor, version-based proof with no age cutoff.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="bootId">Existing public process/clock identifier.</param>
    /// <param name="deviceUtc">Device wall time at issuance.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing public anchor contract.</returns>
    public async Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token = default)
    {
        if (deviceId == Guid.Empty || bootId == Guid.Empty)
            throw new QuestValidationException("Device and process identifiers are required.");
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await RequireAccess(identity, true, token);
        var account = await Account(identity, token);
        var floor = Max(now, account.LastRecordedAt);
        if (floor > now.AddSeconds(5))
            throw new SyncClockNotReadyException("Server time is behind committed account history.");
        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var reconciliation = aggregate.Reconcile(floor, RequestReconciliationLimit);
        if (reconciliation.Processed > 0 || account.HasPendingReconciliation)
        {
            await PersistReconciliation(identity, account, aggregate, floor, now.ToUniversalTime(), RequestReconciliationLimit, reconciliation.HasMore, token);
            if (reconciliation.HasMore)
            {
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                throw new ReconciliationPendingException("Recurring deliveries must finish their bounded reconciliation before issuing the next anchor.");
            }
        }

        var id = Guid.NewGuid();
        await anchorService.SaveAsync(
            account.Id,
            new SyncAnchorEntity
            {
                Id = id,
                AccountId = account.Id,
                DeviceId = deviceId,
                BootId = bootId,
                ServerAt = now.ToUniversalTime(),
                DeviceAt = deviceUtc.ToUniversalTime(),
                RecordedTimeFloorAt = floor,
                IssuedMutationVersion = account.MutationVersion,
                CreatedAt = now.ToUniversalTime(),
                ModifiedAt = now.ToUniversalTime()
            },
            token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(id, deviceId, bootId, now.ToUniversalTime(), deviceUtc, floor);
    }

    /// <summary>Persists one bounded delivery/projection step without creating any client receipt or consuming a client proof.</summary>
    /// <param name="identity">Verified canonical account selected by trusted maintenance.</param>
    /// <param name="now">Maintenance clock.</param>
    /// <param name="maximumDeliveries">One through one thousand due cursor steps.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The processed count and whether more due delivery work remains.</returns>
    public async Task<ReconciliationProgress> Reconcile(AccountIdentity identity, DateTimeOffset now, int maximumDeliveries = RequestReconciliationLimit, CancellationToken token = default)
    {
        if (maximumDeliveries is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        BeginOperation();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await RequireAccess(identity, true, token);
        var account = await Account(identity, token);
        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var at = Max(now, account.LastRecordedAt);
        var progress = aggregate.Reconcile(at, maximumDeliveries);
        var projectionDue = Scheduling.LocalDay(at, account.TimeZoneId) != Scheduling.LocalDay(account.ProjectionAsOfAt, account.TimeZoneId)
            || await accountData.HasDueWorkAsync(account.Id, DateOnly.FromDateTime(at.UtcDateTime), at, false, token);
        if (progress.Processed > 0 || account.HasPendingReconciliation || projectionDue)
            await PersistReconciliation(identity, account, aggregate, at, now.ToUniversalTime(), maximumDeliveries, progress.HasMore, token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return progress;
    }

    /// <inheritdoc />
    public async Task StageLifecyclePauseAsync(AccountIdentity identity, AccountEntity account, DateTimeOffset pausedAt, DateTimeOffset now, CancellationToken token = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A verified lifecycle transaction must own the account lock.");
        var aggregate = await accountService.LoadCurrentAsync(account, token);
        var command = new QuestCommand(Guid.NewGuid(), "$lifecycle-pause");
        var pending = aggregate.Apply(new(command.OperationId, QuestActions.Pause), pausedAt, 0);
        var at = Max(Max(now, account.LastRecordedAt), pausedAt);
        await CommitMutation(identity, account, command, CommandDigest.Compute(command), aggregate, aggregate.Project(at), pausedAt, at, at, now, token, reconciliationLimit: 0, actionReconciliationLimit: 0, internalTransition: true, hasPending: pending.HasMore);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => (a > b ? a : b).ToUniversalTime();

    private Task PersistReconciliation(AccountIdentity identity, AccountEntity account, QuestAggregate aggregate, DateTimeOffset at, DateTimeOffset now, int limit, bool hasMore, CancellationToken token)
    {
        var command = new QuestCommand(Guid.NewGuid(), "$reconcile");
        return CommitMutation(identity, account, command, CommandDigest.Compute(command), aggregate, aggregate.Project(at), at, at, at, now, token, limit, internalTransition: true, hasPending: hasMore);
    }

    private async Task<DateTimeOffset> RecordedAt(AccountEntity account, QuestCommand command, QuestAggregate aggregate, DateTimeOffset receivedAt, DateTimeOffset logicalNow, CancellationToken token)
    {
        if (command.RecordedTime is not { } proof)
        {
            if (logicalNow > receivedAt.AddSeconds(5))
                throw new SyncClockNotReadyException("Server time is behind committed account history.");
            return logicalNow;
        }

        var anchor = await anchorData.FindByIdAsync(proof.AnchorId, token)
            ?? throw new QuestValidationException("Offline time anchor belongs to another account or is unavailable.");
        if (anchor.AccountId != account.Id || anchor.InvalidatedAt is not null || proof.BootId != anchor.BootId || proof.Ordinal <= anchor.LastOrdinal
            || !double.IsFinite(proof.ElapsedMilliseconds) || proof.ElapsedMilliseconds < anchor.LastElapsedMilliseconds || proof.ElapsedMilliseconds < 0)
            throw new QuestValidationException("Offline clock ordering cannot be verified. Preserve the action for reconciliation.");
        DateTimeOffset recorded;
        try
        {
            recorded = anchor.ServerAt.AddMilliseconds(proof.ElapsedMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new QuestValidationException("Offline elapsed time is outside the supported range.");
        }

        if (Math.Abs((proof.DeviceUtc - anchor.DeviceAt).TotalMilliseconds - proof.ElapsedMilliseconds) > TimeSpan.FromMinutes(2).TotalMilliseconds)
            throw new QuestValidationException("The device wall clock changed relative to elapsed time.");

        // Same retry/permanent distinction as reviewed SYNC02 head 7de99e6. No wall-clock expiry.
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The recorded action is ahead of server time; retry the same payload.");
        recorded = Max(recorded, anchor.RecordedTimeFloorAt);
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The fixed anchor floor is ahead of server time.");
        if (command.Action == QuestActions.Complete)
        {
            var occurrence = aggregate.Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new QuestNotFoundException("Occurrence was not found.");
            var anchored = (await historyService.ReplayAsync(account, anchor.IssuedMutationVersion, token)).Aggregate;
            anchored.Reconcile(Max(anchor.ServerAt, anchor.RecordedTimeFloorAt));
            var snapshot = anchored.Occurrences.SingleOrDefault(o => o.Id == occurrence.Id);
            if (snapshot is null && occurrence.Lifecycle?.SourceAnchorId != anchor.Id)
                throw new QuestValidationException("Occurrence was not available in the anchored session.");
            if (occurrence.Lifecycle?.FrozenAt is not null || occurrence.Lifecycle?.AbandonedAt is not null)
                throw new QuestValidationException("Occurrence was paused or abandoned; reconcile the recorded action.");
            if (snapshot is not null && !aggregate.Completions.Any(c => c.OccurrenceId == occurrence.Id && aggregate.Undos.All(u => u.CompletionId != c.Id)))
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(occurrence)] = snapshot with { ParentId = occurrence.ParentId };
        }

        return recorded;
    }

    private async Task<AccountEntity> Account(AccountIdentity identity, CancellationToken token) =>
        await accountData.GetByIdentityUserIdAsync(identity.Subject, token)
        ?? throw new QuestNotFoundException("Initialize a profile before using its quest state.");

    private void BeginOperation()
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Quest operations require their own transaction.");
        db.ChangeTracker.Clear();
    }

    private async Task RequireAccess(AccountIdentity identity, bool commandLock, CancellationToken token)
    {
        IdentityValidation.RequireCanonical(identity);
        if (commandLock)
            await accountData.AcquireCommandLockAsync(identity.Subject, token);
        var marker = await markerData.FindByIdAsync(identity.Subject, token);
        var barrier = await lifecycleData.GetByIdentityUserIdAsync(identity.Subject, token);
        if (marker is not null || (barrier is not null && barrier.StateCode != IdentityProtocol.Active))
            throw new UnauthorizedAccessException("Account lifecycle does not allow product access.");
    }

    private async Task CommitMutation(AccountIdentity identity, AccountEntity account, QuestCommand command, byte[] digest, QuestAggregate aggregate, QuestState state, DateTimeOffset recordedAt, DateTimeOffset reconciledAt, DateTimeOffset projectionAt, DateTimeOffset now, CancellationToken token, int reconciliationLimit = int.MaxValue, int actionReconciliationLimit = int.MaxValue, bool internalTransition = false, bool hasPending = false)
    {
        var change = new QuestCommit(account, command, aggregate, state, recordedAt, reconciledAt, projectionAt, now, reconciliationLimit, actionReconciliationLimit, internalTransition, hasPending);
        if (!internalTransition)
        {
            await receiptService.SaveAsync(
                account.Id,
                new CommandReceiptEntity
                {
                    Id = command.OperationId,
                    AccountId = account.Id,
                    CommandType = command.Action,
                    ApiVersion = 1,
                    DigestVersion = 1,
                    PayloadDigest = digest,
                    OutcomeVersion = 2,
                    OutcomeJson = JsonSerializer.Serialize(new CompactOutcome(projectionAt, null)),
                    AppliedMutationVersion = change.Version,
                    CreatedAt = now
                },
                token);
        }

        await accountService.ApplyProfileAsync(change, token);
        await definitionService.ApplyDefinitionsAsync(change, token);
        await seriesService.ApplySeriesAsync(change, token);
        await occurrenceService.ApplyOccurrencesAsync(change, token);
        await completionService.ApplyCompletionsAsync(change, token);
        await ledgerService.RecalculateAsync(change, token);
        await balanceService.RecalculateAsync(change, token);
        await categoryService.RecalculateAsync(change, token);
        await entitlementService.RecalculateAsync(change, token);
        await historyService.AppendAsync(change, token);
        if (command.RecordedTime is { } proof)
        {
            var anchor = await anchorData.FindByIdAsync(proof.AnchorId, token)
                ?? throw new InvalidOperationException("The validated anchor is unavailable.");
            anchor.LastOrdinal = proof.Ordinal;
            anchor.LastElapsedMilliseconds = proof.ElapsedMilliseconds;
            anchor.ModifiedAt = Max(anchor.ModifiedAt, now);
            await anchorService.SaveAsync(account.Id, anchor, token);
        }

        await auditService.RecordCommandAsync(identity, change, token);
    }

    private sealed record CompactOutcome(DateTimeOffset ProjectionAt, CompletionOutcome? CompletionOutcome);
}
