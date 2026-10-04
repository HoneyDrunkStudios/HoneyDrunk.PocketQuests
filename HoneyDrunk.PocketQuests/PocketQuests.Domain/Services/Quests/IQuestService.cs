using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Synchronization;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Authenticated quest workflows and their explicit transaction boundaries.</summary>
public interface IQuestService
{
    /// <summary>Initializes the account explicitly and idempotently.</summary>
    /// <param name="identity">Verified canonical account identity.</param>
    /// <param name="zone">Initial validated IANA time zone.</param>
    /// <param name="now">Authoritative server time.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the operation.</returns>
    Task Initialize(AccountIdentity identity, string zone, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Projects source history without writing account rows or materialized balances.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Current source-derived state.</returns>
    Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Executes a supported command or reconstructs its exact original response from compact receipt/history.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="command">Immutable client command.</param>
    /// <param name="now">Server receipt clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The original command state, including its original completion feedback.</returns>
    Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Issues a durable fixed-floor, version-based proof with no age cutoff.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="bootId">Existing public process/clock identifier.</param>
    /// <param name="deviceUtc">Device wall time at issuance.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing public anchor contract.</returns>
    Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Persists one bounded delivery/projection step without creating any client receipt or consuming a client proof.</summary>
    /// <param name="identity">Verified canonical account selected by trusted maintenance.</param>
    /// <param name="now">Maintenance clock.</param>
    /// <param name="maximumDeliveries">One through one thousand due cursor steps.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The processed count and whether more due delivery work remains.</returns>
    Task<ReconciliationProgress> Reconcile(AccountIdentity identity, DateTimeOffset now, int maximumDeliveries = 100, CancellationToken token = default);

    /// <summary>Stages a pause inside the verified lifecycle transaction.</summary>
    /// <param name="identity">Verified canonical account identity.</param>
    /// <param name="account">Account owned by the verified identity.</param>
    /// <param name="pausedAt">Authoritative deletion-pause time.</param>
    /// <param name="now">Authoritative server time.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the operation.</returns>
    Task StageLifecyclePauseAsync(AccountIdentity identity, AccountEntity account, DateTimeOffset pausedAt, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Reads one consistent export without account writes or an exclusive command lock.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing version-one private export contract.</returns>
    Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Reads a bounded occurrence page without writing state or taking the command lock.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="after">Exclusive creation cursor.</param>
    /// <param name="size">Page size from one through one hundred.</param>
    /// <param name="now">Host clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current bounded page.</returns>
    Task<QuestOccurrencePage> ReadOccurrences(AccountIdentity identity, int after, int size, DateTimeOffset now, CancellationToken token = default);

    /// <summary>Advances retained backlogs and timed projections without account creation or repeated same-day idle writes.</summary>
    /// <param name="now">Maintenance host clock.</param>
    /// <param name="after">Cursor from the previous bounded page, or null to begin another round.</param>
    /// <param name="maximumAccounts">At most fifty accounts per scan.</param>
    /// <param name="maximumDeliveries">At most one thousand recurrence steps per account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Observed work and the next private scan cursor.</returns>
    Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = 100, CancellationToken token = default);
}
