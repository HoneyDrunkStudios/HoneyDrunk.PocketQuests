using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Quests;

internal static class AccountChanges
{
    internal static void Apply(QuestMutation change, SyncAnchorEntity? anchor)
    {
        var projectionAsOf = change.HasPending
            ? (change.Account.ProjectionAsOfAt < change.RecordedAt ? change.Account.ProjectionAsOfAt : change.RecordedAt)
            : change.ProjectionAt;
        change.ApplyTo(projectionAsOf);
        if (change.Command.RecordedTime is { } proof && anchor is not null)
            QuestAccountMapping.ApplyProof(anchor, proof, change.Now);
    }
}
