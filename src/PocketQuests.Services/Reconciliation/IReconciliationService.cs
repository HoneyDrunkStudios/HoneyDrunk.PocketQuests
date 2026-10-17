using PocketQuests.Contracts.Models.Reconciliation;

namespace PocketQuests.Services.Reconciliation;

/// <summary>Bounded private maintenance over existing accounts.</summary>
public interface IReconciliationService
{
    /// <summary>Advances due sources and projections with one transaction per selected account.</summary>
    /// <param name="now">Trusted maintenance clock.</param>
    /// <param name="after">Previous scan cursor.</param>
    /// <param name="maximumAccounts">Maximum accounts in this page.</param>
    /// <param name="maximumDeliveries">Maximum cursor steps per account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The observed work and next cursor.</returns>
    Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = 100, CancellationToken token = default);
}
