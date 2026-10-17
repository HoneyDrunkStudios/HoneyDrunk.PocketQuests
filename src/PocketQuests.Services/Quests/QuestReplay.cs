using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Services.Quests.Mapping;
using ReplayResult = PocketQuests.Domain.Models.Quests.QuestReplay;

namespace PocketQuests.Services.Quests;

internal static class QuestReplay
{
    internal static ReplayResult Through(QuestReplayRows rows, AccountEntity account, IReadOnlyDictionary<Guid, Quest> terms, long version)
    {
        var history = rows.History.Where(row => row.AccountMutationVersion <= version).ToArray();
        if (history.Length == 0)
        {
            return version == 0 ? new(new QuestAggregate(rows.InitialTimeZone ?? account.TimeZoneId), null)
                : throw new NotSupportedException("The requested historical version has no retained source history.");
        }

        if (history[0].AccountMutationVersion != 1 || history.Length != version)
            throw new NotSupportedException("Incomplete or mixed history requires an explicit conversion before replay.");
        var customKeys = rows.Skills.ToDictionary(row => row.Id, row => row.ClientKey ?? row.Id.ToString("D"));
        var interests = rows.CommandInterests.ToLookup(row => row.QuestCommandHistoryId, row => row.CategoryId);
        var revisions = rows.OccurrenceRevisions.ToDictionary(row => row.Id);
        var aggregate = new QuestAggregate(history[0].TimeZoneBefore);
        CompletionOutcome? outcome = null;
        foreach (var row in history)
        {
            if (row.RulesetVersion != "1.0" || row.TimeZoneBefore != aggregate.Zone)
                throw new NotSupportedException("The original ruleset or coherent historical zone is required for replay.");
            if (row.ReconciliationLimit > 0)
                _ = aggregate.Reconcile(row.ReconciledAt, row.ReconciliationLimit);
            outcome = null;
            if (row.ActionCode == "$reconcile")
            {
                aggregate = RetainProjectedProfile(aggregate, aggregate.Project(row.ProjectionAt).Profile);
                continue;
            }

            if (row.ActionCode == "$lifecycle-pause")
            {
                _ = aggregate.Apply(new(row.Id, QuestActions.Pause), row.RecordedAt, row.ActionReconciliationLimit);
                aggregate = RetainProjectedProfile(aggregate, aggregate.Project(row.ProjectionAt).Profile);
                continue;
            }

            var command = row.ToCommand(terms, customKeys, interests);
            if (row.CompletionTermsRevisionId is { } revisionId)
            {
                var current = aggregate.Occurrences.Single(o => o.Id == row.QuestOccurrenceId);
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(current)] = revisions[revisionId].ToModel(terms, current);
            }

            var before = row.ActionCode == QuestActions.Complete ? aggregate.Project(row.ProjectionAt) : null;
            _ = aggregate.Apply(command, row.RecordedAt, row.ActionReconciliationLimit);
            var projected = aggregate.Project(row.ProjectionAt);
            if (before is not null)
                outcome = CompletionOutcome.Between(before, projected, row.Id, row.QuestOccurrenceId!.Value);

            // The existing store persists the projected profile. Undo can revoke an equipped
            // reward; earning it again does not silently reselect it on a later command/replay.
            aggregate = RetainProjectedProfile(aggregate, projected.Profile);
        }

        return new(aggregate, outcome);
    }

    private static QuestAggregate RetainProjectedProfile(QuestAggregate aggregate, PlayerProfile profile) =>
        aggregate.Profile.BadgeId == profile.BadgeId && aggregate.Profile.FrameId == profile.FrameId
            ? aggregate
            : new(aggregate.Zone, aggregate.Occurrences, aggregate.Completions, aggregate.Undos, aggregate.Definitions, profile, aggregate.Schedule);
}
