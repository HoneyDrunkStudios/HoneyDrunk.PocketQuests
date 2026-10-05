using HoneyDrunk.Audit.Abstractions;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Accounts.Validators;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Profiles;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Quests.Validators;
using PocketQuests.Services.Schedules;
using System.Data;
using System.Diagnostics;
using CommandRequest = PocketQuests.Contracts.Requests.Commands.QuestCommand;
using StateResponse = PocketQuests.Contracts.Responses.Projections.QuestState;

namespace PocketQuests.Services.Quests;

/// <summary>Coordinates authenticated quest operations over pure rules and explicit relational queries.</summary>
/// <param name="data">Scoped account persistence and transaction ownership.</param>
/// <param name="currentAccount">Trusted host identity.</param>
/// <param name="clock">Authoritative host time.</param>
public sealed class QuestService(IAccountDataService data, ICurrentAccount currentAccount, TimeProvider clock) : IQuestService
{
    internal const int ReconciliationLimit = 100;

    /// <inheritdoc />
    public async Task<StateResponse> Execute(CommandRequest request, CancellationToken token = default)
    {
        var errors = QuestValidator.ValidateInput(request);
        if (errors.Count > 0)
            throw new QuestValidationException(string.Join(" ", errors));
        return (await Execute(currentAccount.Identity.ToModel(), request.ToModel(), clock.GetUtcNow(), token)).ToModel();
    }

    /// <inheritdoc />
    public async Task<StateResponse> Read(CancellationToken token = default) =>
        (await Read(currentAccount.Identity.ToModel(), clock.GetUtcNow(), token)).ToModel();

    internal static QuestAggregate Current(AccountEntity account, QuestStateRows rows)
    {
        var terms = QuestTerms.Read(rows);
        var errors = QuestValidator.ValidateSources(rows);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
        return rows.ToModel(account, terms, QuestPauseHistory.Resolve(rows));
    }

    internal async Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token = default)
    {
        IdentityValidation.RequireCanonical(identity);
        var digest = CommandDigest.Compute(command);
        var outcome = await data.ExecuteInTransaction(transactionToken => ExecuteCommand(identity, command, digest, now.ToUniversalTime(), transactionToken), token);
        return outcome ?? throw new ReconciliationPendingException("A bounded recurrence batch committed. Retry this exact command; its proof and receipt were not consumed.");
    }

    internal Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        return data.ExecuteInTransaction(ReadState, token, IsolationLevel.RepeatableRead);

        async Task<QuestState> ReadState(CancellationToken cancellationToken)
        {
            await RequireAccess(identity, false, cancellationToken);
            var account = await Account(identity, cancellationToken);
            var rows = await data.ReadCurrentState(account.Id, cancellationToken);
            var aggregate = Current(account, rows);
            var at = QuestClock.Max(now, account.LastRecordedAt);
            if (aggregate.Reconcile(at, ReconciliationLimit).HasMore)
                throw new ReconciliationPendingException("Recurring deliveries are being reconciled. Retry after maintenance advances the retained backlog.");
            return aggregate.Project(at);
        }
    }

    internal async Task RequireAccess(AccountIdentity identity, bool commandLock, CancellationToken token)
    {
        IdentityValidation.RequireCanonical(identity);
        if (commandLock)
            await data.AcquireCommandLockAsync(identity.Subject, token);
        var marker = await data.GetErasure(identity.Subject, token);
        var lifecycle = await data.GetLifecycle(identity.Subject, token);
        if (marker is not null || (lifecycle is not null && lifecycle.StateCode != IdentityProtocol.Active))
            throw new UnauthorizedAccessException("Account lifecycle does not allow product access.");
    }

    internal async Task<AccountEntity> Account(AccountIdentity identity, CancellationToken token) =>
        await data.GetByIdentityUserIdAsync(identity.Subject, token) ?? throw new QuestNotFoundException("Initialize a profile before using its quest state.");

    internal async Task Stage(AccountIdentity identity, QuestMutation change, QuestStateRows rows, byte[] digest, SyncAnchorEntity? anchor, CancellationToken token)
    {
        var involved = change.Aggregate.Definitions.Select(row => row.Quest)
            .Concat(change.Aggregate.Occurrences.Select(row => row.Quest)).Concat(change.Aggregate.Schedule.Series.Select(row => row.Quest));
        var definitionIds = involved.Select(quest => QuestTermHistory.DefinitionId(change.Account.Id, quest)).Distinct().ToArray();
        var retainedTerms = await data.ReadTerms(change.Account.Id, definitionIds, token);
        var projections = await data.ReadProjections(change.Account.Id, token);
        var changes = CommandHistoryChanges.Create(change, identity, digest, AuditEntryId.New(), Activity.Current?.TraceId.ToString());
        var terms = new QuestTermHistory(change, retainedTerms, changes);
        ProfileChanges.Apply(change, rows, changes);
        terms.ApplyDefinitions();
        QuestSeriesChanges.Apply(change, rows, terms, changes);
        OccurrenceChanges.Apply(change, rows, terms, changes);
        CompletionChanges.Apply(change, rows, changes);
        ProgressChanges.Apply(change, rows, projections, changes);
        QuestHistory.RecordCommand(change, rows, terms, changes);
        AccountChanges.Apply(change, anchor);
        data.Apply(changes);
    }

    internal Task StageReconciliation(AccountIdentity identity, AccountEntity account, QuestStateRows rows, QuestAggregate aggregate, DateTimeOffset at, DateTimeOffset now, int limit, bool hasMore, CancellationToken token)
    {
        var command = new QuestCommand(Guid.NewGuid(), "$reconcile");
        var change = new QuestMutation(account, command, aggregate, aggregate.Project(at), at, at, at, now, limit, int.MaxValue, IsInternal: true, HasPending: hasMore);
        return Stage(identity, change, rows, CommandDigest.Compute(command), null, token);
    }

    internal async Task StageLifecyclePause(AccountIdentity identity, AccountEntity account, DateTimeOffset pausedAt, DateTimeOffset now, CancellationToken token)
    {
        var rows = await data.ReadCurrentState(account.Id, token);
        var aggregate = Current(account, rows);
        var command = new QuestCommand(Guid.NewGuid(), "$lifecycle-pause");
        var pending = aggregate.Apply(new(command.OperationId, QuestActions.Pause), pausedAt, 0);
        var at = QuestClock.Max(QuestClock.Max(now, account.LastRecordedAt), pausedAt);
        var change = new QuestMutation(account, command, aggregate, aggregate.Project(at), pausedAt, at, at, now, 0, 0, IsInternal: true, HasPending: pending.HasMore);
        await Stage(identity, change, rows, CommandDigest.Compute(command), null, token);
    }

    private static void RequireMatchingReceipt(CommandReceiptEntity? receipt, AccountEntity account, QuestCommand command, byte[] digest)
    {
        if (receipt is null)
            return;
        if (receipt.AccountId != account.Id)
            throw new QuestConflictException("Operation ID already belongs to another account.");
        if (receipt.ApiVersion != 1 || receipt.DigestVersion != 1 || receipt.OutcomeVersion != 2)
            throw new NotSupportedException("The stored receipt version requires its original replay implementation.");
        if (!receipt.PayloadDigest.AsSpan().SequenceEqual(digest) || receipt.CommandType != command.Action)
            throw new QuestConflictException("Operation ID already belongs to a different payload.");
    }

    private async Task<QuestState?> ExecuteCommand(AccountIdentity identity, QuestCommand command, byte[] digest, DateTimeOffset now, CancellationToken token)
    {
        await RequireAccess(identity, true, token);
        var account = await Account(identity, token);
        var receipt = await data.GetReceipt(command.OperationId, token);
        RequireMatchingReceipt(receipt, account, command, digest);
        if (receipt is not null)
        {
            var retained = await data.ReadReplay(account.Id, receipt.AppliedMutationVersion, token);
            var original = QuestReplay.Through(retained, account, QuestTerms.Read(retained), receipt.AppliedMutationVersion);
            return original.Aggregate.Project(receipt.ToOutcome().ProjectionAt) with { CompletionOutcome = original.Outcome };
        }

        var rows = await data.ReadCurrentState(account.Id, token);
        var aggregate = Current(account, rows);
        var logicalNow = QuestClock.Max(now, account.LastRecordedAt);
        var reconciliation = aggregate.Reconcile(logicalNow, ReconciliationLimit);
        if (reconciliation.HasMore)
        {
            var internalCommand = new QuestCommand(Guid.NewGuid(), "$reconcile");
            var continuation = new QuestMutation(account, internalCommand, aggregate, aggregate.Project(logicalNow), logicalNow, logicalNow, logicalNow, now, ReconciliationLimit, int.MaxValue, IsInternal: true, HasPending: true);
            await Stage(identity, continuation, rows, CommandDigest.Compute(internalCommand), null, token);
            return null;
        }

        var anchor = command.RecordedTime is { } proof ? await data.GetAnchor(account.Id, proof.AnchorId, token) : null;
        QuestAggregate? anchored = null;
        if (anchor is not null && command.Action == QuestActions.Complete)
        {
            var retained = await data.ReadReplay(account.Id, anchor.IssuedMutationVersion, token);
            anchored = QuestReplay.Through(retained, account, QuestTerms.Read(retained), anchor.IssuedMutationVersion).Aggregate;
        }

        var recordedAt = QuestClock.Resolve(account, command, aggregate, anchored, anchor, now, logicalNow);
        var projectionAt = QuestClock.Max(logicalNow, recordedAt);
        var before = command.Action == QuestActions.Complete ? aggregate.Project(projectionAt) : null;
        var actionBudget = ReconciliationLimit - reconciliation.Processed;
        var action = aggregate.Apply(command, recordedAt, actionBudget);
        var result = aggregate.Project(projectionAt);
        if (before is not null)
            result = result with { CompletionOutcome = CompletionOutcome.Between(before, result, command.OperationId, command.OccurrenceId!.Value) };
        var change = new QuestMutation(account, command, aggregate, result, recordedAt, logicalNow, projectionAt, now, ReconciliationLimit, actionBudget, HasPending: action.HasMore || aggregate.Reconcile(projectionAt, 0).HasMore);
        await Stage(identity, change, rows, digest, anchor, token);
        return result;
    }
}
