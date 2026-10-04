using PocketQuests.Domain.Models.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Transaction-local changes, saved with the command receipt for response-loss retries.</summary>
public record CompletionOutcome(Guid CompletionId, Guid OccurrenceId,
    ImmutableArray<CompletionLevelUp> LevelUps, Rank? RankUp, ImmutableArray<Entitlement> Unlocks)
{
    /// <summary>Compares authoritative projections under the account lock; never derives rewards from client state.</summary>
    /// <param name="before">Projection immediately before applying the completion.</param>
    /// <param name="after">Projection immediately after applying the completion.</param>
    /// <param name="completionId">The command operation ID that identifies this completion.</param>
    /// <param name="occurrenceId">The occurrence completed by the command.</param>
    /// <returns>The new completion's changes, or null for a no-op.</returns>
    public static CompletionOutcome? Between(QuestState before, QuestState after, Guid completionId, Guid occurrenceId)
    {
        if (before.Occurrences.Any(o => o.Completion?.Id == completionId)
            || !after.Occurrences.Any(o => o.Occurrence.Id == occurrenceId && o.Completion?.Id == completionId)
            || !after.Ledger.Any(e => e.EventId == completionId && e.OccurrenceId == occurrenceId && e.Track == "Overall"))
            return null;

        var levels = ImmutableArray.CreateBuilder<CompletionLevelUp>();
        if (after.OverallLevel > before.OverallLevel)
            levels.Add(new("Overall", "overall", "Overall", before.OverallLevel, after.OverallLevel));
        Add("Category", before.Categories, after.Categories);
        Add("Attribute", before.Attributes, after.Attributes);
        Add("Skill", before.Skills, after.Skills);
        return new(
            completionId,
            occurrenceId,
            levels.ToImmutable(),
            after.Rank.Current > before.Rank.Current ? after.Rank.Current : null,
            [.. after.Entitlements.Where(e => e.Earned && !before.Entitlements.Any(b => b.Id == e.Id && b.Earned))]);

        void Add(string track, ImmutableArray<Balance> previous, ImmutableArray<Balance> next)
        {
            foreach (var balance in next)
            {
                var prior = previous.FirstOrDefault(b => b.Id == balance.Id);
                if (prior is not null && balance.Level > prior.Level)
                    levels.Add(new(track, balance.Id, balance.Name, prior.Level, balance.Level));
            }
        }
    }
}
