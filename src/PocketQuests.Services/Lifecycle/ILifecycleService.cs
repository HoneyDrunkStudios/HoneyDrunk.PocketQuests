using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;

namespace PocketQuests.Services.Lifecycle;

/// <summary>Private lifecycle persistence selected together with the product store.</summary>
public interface ILifecycleService
{
    /// <summary>Fences product access using the current verified Identity response.</summary>
    /// <param name="user">Authoritative account resolution.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after any required transition.</returns>
    Task ObserveActive(UserRecord user, CancellationToken token = default);

    /// <summary>Commits a private transition and its owned acknowledgment envelope atomically.</summary>
    /// <param name="intent">Verified private instruction.</param>
    /// <param name="acknowledgmentQueue">Trusted host destination.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after durable commit.</returns>
    Task Receive(LifecycleIntent intent, string acknowledgmentQueue, CancellationToken token = default);

    /// <summary>Removes expired minimal markers and owned acknowledgment envelopes.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of retention cleanup.</returns>
    Task Prune(CancellationToken token = default);
}
