using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Accounts.Validators;

namespace PocketQuests.Services.Lifecycle.Validators;

internal static class LifecycleValidator
{
    internal const int ErasureRetentionDays = 35;
    private const int ClockSkewSeconds = 30;
    private const int MaximumIntentHours = 1;
    private const int AcknowledgmentLength = 64;
    private const string InactiveAccount = "Account is inactive or its lifecycle is incomplete.";
    private const string ChangedLifecycle = "Account lifecycle changed; sign in again.";
    private const string InvalidIntent = "Invalid lifecycle instruction.";
    private const string InvalidClock = "Invalid lifecycle clock.";
    private const string InvalidMarker = "A current verified marker is required.";

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
            || user.DeletionPausedAt > now.AddSeconds(ClockSkewSeconds))
            throw new UnauthorizedAccessException(InactiveAccount);
    }

    internal static void ActiveVersion(UserRecord user, AccountLifecycleStateEntity? barrier)
    {
        if (barrier is not null && (barrier.Version > user.LifecycleVersion || (barrier.Version == user.LifecycleVersion && barrier.StateCode != IdentityProtocol.Active)))
            throw new UnauthorizedAccessException(ChangedLifecycle);
    }

    internal static void Intent(LifecycleIntent intent, string queue)
    {
        if (intent.Consumer != IdentityProtocol.ConsumerId || intent.State is not (IdentityProtocol.Active or IdentityProtocol.Inactive or IdentityProtocol.Erasing)
            || intent.Version <= 0 || intent.Acknowledgment?.Length != AcknowledgmentLength || string.IsNullOrWhiteSpace(queue))
            throw new UnauthorizedAccessException(InvalidIntent);
    }

    internal static void IntentClock(LifecycleIntent intent, DateTimeOffset now)
    {
        if (intent.EffectiveAt > now.AddSeconds(ClockSkewSeconds) || intent.PausedAt > intent.EffectiveAt || intent.ExpiresAt > now.AddHours(MaximumIntentHours).AddSeconds(ClockSkewSeconds))
            throw new ArgumentException(InvalidClock);
    }

    internal static void Erasure(DateTimeOffset originalErasedAt, DateTimeOffset now)
    {
        if (originalErasedAt > now || originalErasedAt <= now.AddDays(-ErasureRetentionDays))
            throw new ArgumentException(InvalidMarker);
    }
}
