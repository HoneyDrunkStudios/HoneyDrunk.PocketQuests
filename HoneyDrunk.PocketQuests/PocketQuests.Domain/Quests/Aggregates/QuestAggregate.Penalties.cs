using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using System.Text.Json;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Explicitly accepted category losses and deterministic chronological replay.</summary>
public sealed partial class QuestAggregate
{
    private static int Loss(Quest quest) => ((quest.BaseXp * quest.PenaltyPercent) + 50) / 100;

    private static OccurrenceLifecycle? PenaltyTerms(Quest quest, QuestCommand command, DateTimeOffset? deadline)
    {
        if (quest.PenaltyPercent == 0)
            return null;
        Progression.Require(deadline is not null, "Penalty acceptance requires a due day.");
        Progression.Require(command.ConfirmPenalty && command.AcceptedLoss == Loss(quest), "Confirm the displayed exact category loss before accepting a penalty quest.");
        Progression.Require(command.AcceptedQuest is not null && JsonSerializer.Serialize(command.AcceptedQuest) == JsonSerializer.Serialize(quest), "Penalty terms changed. Review the current quest and confirm again.");
        return new(LockedLoss: Loss(quest), LossCategoryId: quest.CategoryId);
    }

    private DateTimeOffset? AssessmentTime(Occurrence occurrence, DateTimeOffset now)
    {
        var life = occurrence.Lifecycle;
        if (life?.LockedLoss is null || life.Unaccepted || Surviving(occurrence.Id, now) is not null)
            return null;
        DateTimeOffset? at = life.AbandonedAt <= now ? life.AbandonedAt : null;
        if (life.FrozenAt is null && occurrence.Deadline is { } deadline && now >= deadline)
        {
            var assessment = deadline;
            var lastCompletion = Completions.Where(c => c.OccurrenceId == occurrence.Id && c.RecordedAt <= now).OrderBy(c => c.RecordedAt).LastOrDefault();
            var undo = lastCompletion is null ? null : Undos.SingleOrDefault(u => u.CompletionId == lastCompletion.Id && u.RecordedAt <= now);
            if (undo is not null && undo.RecordedAt > deadline)
                assessment = undo.RecordedAt;
            if (at is null || assessment < at)
                at = assessment;
        }

        return at <= now ? at : null;
    }

    private void AcceptOffer(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
        Progression.Require(occurrence.Lifecycle?.Unaccepted == true && occurrence.Deadline > now && !IsPaused(occurrence.Quest.CategoryId), "This offer is no longer available.");
        RequireEligible(occurrence.Quest, now);
        var terms = PenaltyTerms(occurrence.Quest, command, occurrence.Deadline);
        Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { Lifecycle = occurrence.Lifecycle! with { Unaccepted = false, LockedLoss = terms?.LockedLoss, LossCategoryId = terms?.LossCategoryId } };
    }
}
