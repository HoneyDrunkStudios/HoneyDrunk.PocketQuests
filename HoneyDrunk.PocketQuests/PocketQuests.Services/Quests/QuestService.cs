using HoneyDrunk.Audit.Abstractions;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services.Accounts;
using PocketQuests.Domain.Services.Synchronization;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Quests.Validators;
using System.Diagnostics;
using System.Text.Json;
using CommandRequest = PocketQuests.Contracts.Requests.Commands.QuestCommand;
using StateResponse = PocketQuests.Contracts.Responses.Projections.QuestState;

namespace PocketQuests.Services.Quests;

/// <summary>Coordinates the completion transaction while retaining other operations on their existing implementation.</summary>
/// <param name="data">Scoped completion entity persistence.</param>
/// <param name="currentAccount">Trusted host identity.</param>
/// <param name="clock">Authoritative host time.</param>
/// <param name="remainingOperations">Unmigrated operations, retained until another slice is approved.</param>
public sealed class QuestService(IQuestCompletionDataService data, ICurrentAccount currentAccount, TimeProvider clock,
    PocketQuests.Domain.Services.Quests.IQuestService remainingOperations) : IQuestService
{
    private const int ReconciliationLimit = 100;

    /// <inheritdoc />
    public async Task<StateResponse> Execute(CommandRequest request, CancellationToken token = default)
    {
        var errors = QuestValidator.ValidateInput(request);
        if (errors.Count > 0)
            throw new QuestValidationException(string.Join(" ", errors));
        var identity = currentAccount.Identity.ToModel();
        IdentityValidation.RequireCanonical(identity);
        var command = request.ToModel();
        var now = clock.GetUtcNow();
        if (command.Action != QuestActions.Complete)
            return (await remainingOperations.Execute(identity, command, now, token)).ToModel();

        var outcome = await data.ExecuteInTransaction(transactionToken => Complete(identity, command, now, transactionToken), token);
        if (outcome is null)
            throw new ReconciliationPendingException("A bounded recurrence batch committed. Retry this exact command; its proof and receipt were not consumed.");
        return outcome.ToModel();
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

    private static QuestState ReplayReceipt(QuestCompletionRows rows, AccountEntity account, IReadOnlyDictionary<Guid, Quest> terms, CommandReceiptEntity receipt)
    {
        var outcome = JsonSerializer.Deserialize<CompactOutcome>(receipt.OutcomeJson) ?? throw new InvalidOperationException("Receipt is invalid.");
        var original = QuestReplay.Through(rows, account, terms, receipt.AppliedMutationVersion);
        return original.Aggregate.Project(outcome.ProjectionAt) with { CompletionOutcome = original.Outcome };
    }

    private async Task<QuestState?> Complete(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token)
    {
        await data.AcquireAccountLock(identity.Subject, token);
        var marker = await data.GetErasure(identity.Subject, token);
        var lifecycle = await data.GetLifecycle(identity.Subject, token);
        if (marker is not null || (lifecycle is not null && lifecycle.StateCode != IdentityProtocol.Active))
            throw new UnauthorizedAccessException("Account lifecycle does not allow product access.");
        var account = await data.GetAccount(identity.Subject, token) ?? throw new QuestNotFoundException("Initialize a profile before using its quest state.");
        var digest = CommandDigest.Compute(command);
        var receipt = await data.GetReceipt(command.OperationId, token);
        RequireMatchingReceipt(receipt, account, command, digest);
        var rows = await data.GetCompletionRows(account.Id, account.MutationVersion, token);
        var terms = rows.ToTerms();
        var sourceErrors = QuestValidator.ValidateSources(rows, terms);
        if (sourceErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", sourceErrors));
        if (receipt is not null)
            return ReplayReceipt(rows, account, terms, receipt);

        var aggregate = rows.ToModel(account, terms, QuestPauseHistory.Resolve(rows));
        var logicalNow = QuestClock.Max(now, account.LastRecordedAt);
        var reconciliation = aggregate.Reconcile(logicalNow, ReconciliationLimit);
        if (reconciliation.HasMore)
        {
            var internalCommand = new QuestCommand(Guid.NewGuid(), "$reconcile");
            var continuation = new QuestCommit(account, internalCommand, aggregate, aggregate.Project(logicalNow), logicalNow, logicalNow, logicalNow, now, ReconciliationLimit, int.MaxValue, IsInternal: true, HasPending: true);
            Stage(identity, continuation, rows, terms, CommandDigest.Compute(internalCommand), null);
            return null;
        }

        var anchor = command.RecordedTime is { } proof ? await data.GetAnchor(account.Id, proof.AnchorId, token) : null;
        var recordedAt = QuestClock.Resolve(account, command, aggregate, rows, terms, anchor, now, logicalNow);
        var projectionAt = QuestClock.Max(logicalNow, recordedAt);
        var before = aggregate.Project(projectionAt);
        var actionBudget = ReconciliationLimit - reconciliation.Processed;
        var action = aggregate.Apply(command, recordedAt, actionBudget);
        var result = aggregate.Project(projectionAt);
        result = result with { CompletionOutcome = CompletionOutcome.Between(before, result, command.OperationId, command.OccurrenceId!.Value) };
        var change = new QuestCommit(account, command, aggregate, result, recordedAt, logicalNow, projectionAt, now, ReconciliationLimit, actionBudget, HasPending: action.HasMore || aggregate.Reconcile(projectionAt, 0).HasMore);
        Stage(identity, change, rows, terms, digest, anchor);
        return result;
    }

    private void Stage(AccountIdentity identity, QuestCommit change, QuestCompletionRows rows, IReadOnlyDictionary<Guid, Quest> terms, byte[] digest, SyncAnchorEntity? anchor)
    {
        var changes = change.ToChanges(rows, identity, digest, AuditEntryId.New(), Activity.Current?.TraceId.ToString());
        CompletionOccurrenceMapping.ApplyTo(change, rows, terms, changes);
        change.ApplyCompletions(rows, changes);
        CompletionProgressMapping.ApplyTo(change, rows, changes);
        CompletionAccountMapping.ApplyTo(change, rows, anchor);
        data.Apply(changes);
    }
}
