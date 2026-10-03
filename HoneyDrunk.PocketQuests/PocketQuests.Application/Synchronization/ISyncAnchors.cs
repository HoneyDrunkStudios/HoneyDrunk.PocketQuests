using PocketQuests.Application.Identity;

namespace PocketQuests.Application.Synchronization;

/// <summary>Issues trusted device time anchors with an immutable product snapshot.</summary>
public interface ISyncAnchors
{
    /// <summary>Captures the current account snapshot and device clock baseline atomically.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="deviceId">Installation identifier.</param>
    /// <param name="bootId">Current device process identifier.</param>
    /// <param name="deviceUtc">Device wall clock at anchor request.</param>
    /// <param name="now">Server clock at anchor issuance.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>The account-bound anchor.</returns>
    Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token);
}
