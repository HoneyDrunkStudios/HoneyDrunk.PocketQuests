using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Services.Exports;
using PocketQuests.Services.Profiles;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Reconciliation;
using PocketQuests.Services.Synchronization;

namespace PocketQuests.Tests.Fixtures;

/// <summary>Supplies explicit test identities and clocks to the real feature services; contains no persistence or rules.</summary>
/// <param name="quests">Production command and read workflow.</param>
/// <param name="profiles">Production profile initialization.</param>
/// <param name="anchors">Production anchor issuance.</param>
/// <param name="maintenance">Production bounded reconciliation.</param>
/// <param name="exports">Production consistent export.</param>
/// <param name="occurrences">Production bounded pages.</param>
internal sealed class TestQuestWorkflow(QuestService quests, ProfileService profiles, SynchronizationService anchors, ReconciliationService maintenance, ExportService exports, OccurrenceReadService occurrences)
{
    public async Task<QuestState> Initialize(AccountIdentity identity, string zone, DateTimeOffset now, CancellationToken token = default)
    {
        await profiles.Initialize(identity, zone, now, token);
        return await quests.Read(identity, now, token);
    }

    public Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default) => quests.Read(identity, now, token);

    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token = default) => quests.Execute(identity, command, now, token);

    public Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token = default) =>
        anchors.CreateAnchor(identity, deviceId, bootId, deviceUtc, now, token);

    public Task<ReconciliationProgress> Reconcile(AccountIdentity identity, DateTimeOffset now, int maximumDeliveries = 100, CancellationToken token = default) =>
        maintenance.Reconcile(identity, now, maximumDeliveries, token);

    public Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default) => exports.Read(identity, now, token);

    public Task<QuestOccurrencePage> ReadOccurrences(AccountIdentity identity, int after, int size, DateTimeOffset now, CancellationToken token = default) => occurrences.Read(identity, after, size, now, token);

    public async Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = 100, CancellationToken token = default)
    {
        var batch = await maintenance.ReconcileAccounts(now, after is null ? null : new(after.ProjectionAsOfAt, after.AccountId), maximumAccounts, maximumDeliveries, token);
        return new(batch.Scanned, batch.Reconciled, batch.Deliveries, batch.Next is null ? null : new(batch.Next.ProjectionAsOfAt, batch.Next.AccountId));
    }
}
