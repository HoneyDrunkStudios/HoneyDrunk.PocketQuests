using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Accounts.Validators;

namespace PocketQuests.Services.Lifecycle.Validators;

internal static class LifecycleValidator
{
    internal static AccountIdentity Identity(string userId)
    {
        var identity = new AccountIdentity("honeydrunk-identity", userId);
        IdentityValidation.RequireCanonical(identity);
        return identity;
    }

    internal static void Active(UserRecord user, DateTimeOffset now)
    {
        if (user.State != IdentityProtocol.Active || user.LifecycleVersion < 0
            || (user.LifecycleVersion > 0 && user.DeletionPausedAt is null)
            || user.DeletionPausedAt > now.AddSeconds(30))
            throw new UnauthorizedAccessException("Account is inactive or its lifecycle is incomplete.");
    }

    internal static void ActiveVersion(UserRecord user, AccountLifecycleStateEntity? barrier)
    {
        if (barrier is not null && (barrier.Version > user.LifecycleVersion || (barrier.Version == user.LifecycleVersion && barrier.StateCode != IdentityProtocol.Active)))
            throw new UnauthorizedAccessException("Account lifecycle changed; sign in again.");
    }

    internal static void Intent(LifecycleIntent intent, string queue)
    {
        if (intent.Consumer != IdentityProtocol.ConsumerId || intent.State is not (IdentityProtocol.Active or IdentityProtocol.Inactive or IdentityProtocol.Erasing)
            || intent.Version <= 0 || intent.Acknowledgment?.Length != 64 || string.IsNullOrWhiteSpace(queue))
            throw new UnauthorizedAccessException("Invalid lifecycle instruction.");
    }

    internal static void IntentClock(LifecycleIntent intent, DateTimeOffset now)
    {
        if (intent.EffectiveAt > now.AddSeconds(30) || intent.PausedAt > intent.EffectiveAt || intent.ExpiresAt > now.AddHours(1).AddSeconds(30))
            throw new ArgumentException("Invalid lifecycle clock.");
    }

    internal static void Erasure(DateTimeOffset originalErasedAt, DateTimeOffset now)
    {
        if (originalErasedAt > now || originalErasedAt <= now.AddDays(-35))
            throw new ArgumentException("A current verified marker is required.");
    }
}
