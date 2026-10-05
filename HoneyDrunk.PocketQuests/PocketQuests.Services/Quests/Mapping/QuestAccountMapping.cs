using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Models.Synchronization;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestAccountMapping
{
    internal static void ApplyTo(this QuestMutation change, DateTimeOffset projectionAsOf)
    {
        var account = change.Account;
        account.TimeZoneId = change.Aggregate.Zone;
        account.IsOnboardingComplete = change.Aggregate.Profile.OnboardingComplete;
        account.HasExpiryWarnings = change.Aggregate.Profile.ExpiryWarnings;
        account.IsAccountPaused = change.Aggregate.Schedule.AccountPaused;
        account.SelectedBadgeId = change.State.Profile.BadgeId;
        account.SelectedFrameId = change.State.Profile.FrameId;
        account.MutationVersion = change.Version;
        account.ProjectionVersion = change.Version;
        account.ProjectionAsOfAt = projectionAsOf;
        account.HasPendingReconciliation = change.HasPending;
        account.LastRecordedAt = change.ProjectionAt;
    }

    internal static void ApplyProof(SyncAnchorEntity anchor, RecordedActionTime proof)
    {
        anchor.LastOrdinal = proof.Ordinal;
        anchor.LastElapsedMilliseconds = proof.ElapsedMilliseconds;
    }
}
