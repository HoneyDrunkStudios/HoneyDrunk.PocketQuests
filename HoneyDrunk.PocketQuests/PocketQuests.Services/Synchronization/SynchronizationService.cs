using PocketQuests.Contracts.Requests.Synchronization;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Synchronization.Mapping;
using AnchorResponse = PocketQuests.Contracts.Models.Synchronization.SyncAnchor;

namespace PocketQuests.Services.Synchronization;

/// <summary>Owns atomic anchor issuance after bounded retained recurrence delivery.</summary>
/// <param name="data">Account transaction and source queries.</param>
/// <param name="anchors">Scoped anchor persistence.</param>
/// <param name="quests">Shared source and mutation preparation.</param>
/// <param name="currentAccount">Authenticated identity.</param>
/// <param name="clock">Authoritative host time.</param>
public sealed class SynchronizationService(IAccountDataService data, ISyncAnchorDataService anchors, QuestService quests, ICurrentAccount currentAccount, TimeProvider clock) : ISynchronizationService
{
    /// <inheritdoc />
    public async Task<AnchorResponse> CreateAnchor(AnchorRequest request, CancellationToken token = default) =>
        (await CreateAnchor(currentAccount.Identity.ToModel(), request.DeviceId, request.BootId, request.DeviceUtc, clock.GetUtcNow(), token)).ToModel();

    internal async Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token = default)
    {
        if (deviceId == Guid.Empty || bootId == Guid.Empty)
            throw new QuestValidationException("Device and process identifiers are required.");
        var result = await data.ExecuteInTransaction(Create, token);
        return result ?? throw new ReconciliationPendingException("Recurring deliveries must finish their bounded reconciliation before issuing the next anchor.");

        async Task<SyncAnchor?> Create(CancellationToken cancellationToken)
        {
            await quests.RequireAccess(identity, true, cancellationToken);
            var account = await quests.Account(identity, cancellationToken);
            var floor = QuestClock.Max(now, account.LastRecordedAt);
            if (floor > now.AddSeconds(5))
                throw new SyncClockNotReadyException("Server time is behind committed account history.");
            var rows = await data.ReadCurrentState(account.Id, cancellationToken);
            var aggregate = QuestService.Current(account, rows);
            var progress = aggregate.Reconcile(floor, QuestService.ReconciliationLimit);
            if (progress.Processed > 0 || account.HasPendingReconciliation)
            {
                await quests.StageReconciliation(identity, account, rows, aggregate, floor, now.ToUniversalTime(), QuestService.ReconciliationLimit, progress.HasMore, cancellationToken);
                if (progress.HasMore)
                    return null;
            }

            var anchor = AnchorPersistenceMapping.Create(account, deviceId, bootId, deviceUtc, now, floor);
            await anchors.AddAsync(anchor, cancellationToken);
            return new(anchor.Id, deviceId, bootId, now.ToUniversalTime(), deviceUtc, floor);
        }
    }
}
