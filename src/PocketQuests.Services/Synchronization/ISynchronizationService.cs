using PocketQuests.Contracts.Models.Synchronization;
using PocketQuests.Contracts.Requests.Synchronization;

namespace PocketQuests.Services.Synchronization;

/// <summary>Issues durable recorded-time proofs for the authenticated account.</summary>
public interface ISynchronizationService
{
    /// <summary>Reconciles bounded due work before issuing a fixed-floor anchor.</summary>
    /// <param name="request">Device, process and wall-clock observation.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The new anchor, with no age cutoff.</returns>
    Task<SyncAnchor> CreateAnchor(AnchorRequest request, CancellationToken token = default);
}
