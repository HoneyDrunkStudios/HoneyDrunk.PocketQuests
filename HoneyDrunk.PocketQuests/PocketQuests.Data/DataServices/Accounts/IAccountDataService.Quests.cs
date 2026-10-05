using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>Operation-specific account reads and staging within the shared unit of work.</summary>
public partial interface IAccountDataService
{
    /// <summary>The current private lifecycle barrier.</summary>
    /// <param name="identityUserId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<AccountLifecycleStateEntity?> GetLifecycle(string identityUserId, CancellationToken token);

    /// <summary>The original retained erasure marker.</summary>
    /// <param name="identityUserId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<ErasureMarkerEntity?> GetErasure(string identityUserId, CancellationToken token);

    /// <summary>The globally addressed receipt; Services must verify ownership before use.</summary>
    /// <param name="operationId">Verified operation query key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<CommandReceiptEntity?> GetReceipt(Guid operationId, CancellationToken token);

    /// <summary>The owned recorded-clock anchor.</summary>
    /// <param name="accountId">Resolved account owner.</param>
    /// <param name="anchorId">Recorded-clock anchor identifier.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<SyncAnchorEntity?> GetAnchor(Guid accountId, Guid anchorId, CancellationToken token);

    /// <summary>Current source rows, excluding receipt payloads, replay history and reward projection rows.</summary>
    /// <param name="accountId">Resolved account owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<QuestStateRows> ReadCurrentState(Guid accountId, CancellationToken token);

    /// <summary>Only retained sources for the requested replay version.</summary>
    /// <param name="accountId">Resolved account owner.</param>
    /// <param name="version">Last included mutation version.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<QuestReplayRows> ReadReplay(Guid accountId, long version, CancellationToken token);

    /// <summary>Tracked reward projections for a mutation; never needed for read-only replay.</summary>
    /// <param name="accountId">Resolved account owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected relational rows.</returns>
    Task<QuestProjectionRows> ReadProjections(Guid accountId, CancellationToken token);

    /// <summary>Reads retained terms for explicitly selected owned definitions.</summary>
    /// <param name="accountId">Resolved owner.</param>
    /// <param name="definitionIds">Definitions involved in the operation.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Selected immutable terms and their stable keys.</returns>
    Task<QuestTermsRows> ReadTerms(Guid accountId, Guid[] definitionIds, CancellationToken token);

    /// <summary>Reads only immutable terms referenced by a bounded occurrence page.</summary>
    /// <param name="accountId">Resolved owner.</param>
    /// <param name="revisionIds">Selected immutable revision identifiers.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The selected terms and allocation identities.</returns>
    Task<QuestTermsRows> ReadSelectedTerms(Guid accountId, Guid[] revisionIds, CancellationToken token);

    /// <summary>Reads custom definition history required by the private export contract.</summary>
    /// <param name="accountId">Resolved owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Retained terms with archive provenance.</returns>
    Task<QuestDefinitionHistoryRows> ReadDefinitionHistory(Guid accountId, CancellationToken token);

    /// <summary>Stages an explicit entity batch in the current operation transaction.</summary>
    /// <param name="changes">New and removed entities selected by Services.</param>
    void Apply(QuestChanges changes);
}
