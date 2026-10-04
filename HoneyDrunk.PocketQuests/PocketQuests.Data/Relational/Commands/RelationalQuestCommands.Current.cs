using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Collections.Immutable;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Loads typed current state directly; historical command re-execution is reserved for receipt/anchor replay.</summary>
public sealed partial class RelationalQuestCommands
{
    private static async Task<QuestAggregate> LoadCurrent(Session session, AccountEntity account, CancellationToken token)
    {
        var db = session.Context;
        var terms = await ReadTerms(session, account.Id, token);
        var definitions = await db.Set<QuestDefinitionEntity>().Where(r => r.AccountId == account.Id && r.SystemQuestId == null).OrderBy(r => r.CreationOrdinal).ToListAsync(token);
        var definitionRevisions = await db.Set<QuestDefinitionRevisionEntity>().Where(r => r.AccountId == account.Id).ToListAsync(token);
        var skills = await db.Set<CustomSkillEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToListAsync(token);
        var receipts = await db.Set<CommandReceiptEntity>().Where(r => r.AccountId == account.Id).Select(r => new { r.Id, r.AppliedMutationVersion }).ToDictionaryAsync(r => r.Id, r => r.AppliedMutationVersion, token);
        var assessments = (await db.Set<SkillAssessmentEntity>().Where(r => r.AccountId == account.Id).ToListAsync(token)).OrderBy(r => receipts[r.CommandReceiptId]).ToArray();
        var zones = (await db.Set<TimeZoneChangeEntity>().Where(r => r.AccountId == account.Id).ToListAsync(token)).OrderBy(r => receipts[r.CommandReceiptId]).ToArray();
        var interests = await db.Set<AccountInterestEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.Position).Select(r => r.CategoryId).ToListAsync(token);
        var assessmentHistory = assessments.Select(r => new SkillAssessment(r.SystemSkillId ?? SkillKey(r.CustomSkillId!.Value), Enum.Parse<Experience>(r.ExperienceCode), r.EffectiveAt)).ToImmutableList();
        var assessed = ImmutableDictionary<string, Experience>.Empty;
        foreach (var assessment in assessmentHistory)
            assessed = assessed.SetItem(assessment.SkillId, assessment.Experience);
        var profile = new PlayerProfile(
            [.. interests],
            assessed,
            account.IsOnboardingComplete,
            account.SelectedBadgeId,
            account.SelectedFrameId,
            skills.Count == 0 ? null : [.. skills.Select(r => new CustomSkill(r.ClientKey ?? r.Id.ToString("D"), r.Name, r.Revision, r.ArchivedAt is not null))],
            assessmentHistory.Count == 0 ? null : assessmentHistory,
            zones.Length == 0 ? null : [.. zones.Select(r => new ZoneChange(r.FromTimeZoneId, r.ToTimeZoneId, r.EffectiveAt))],
            account.HasExpiryWarnings);
        var seriesRows = await db.Set<QuestSeriesEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToListAsync(token);
        var seriesRevisions = await db.Set<QuestSeriesRevisionEntity>().Where(r => r.AccountId == account.Id).ToDictionaryAsync(r => r.Id, token);
        var series = seriesRows.Select(r =>
        {
            var revision = seriesRevisions.Values.Single(v => v.QuestSeriesId == r.Id && v.Revision == r.Revision);
            return new QuestSeries(r.Id, terms[revision.QuestDefinitionRevisionId], DateText(revision.AnchorOn)!, Enum.Parse<Cadence>(revision.CadenceCode), revision.Interval, revision.ScheduleVersion, r.NextSequence, r.PauseDays, r.StoppedAt is not null, TimeText(revision.PlannedTime), revision.HasAutoAcceptPenalty, revision.EffectiveAt);
        }).ToImmutableArray();
        var pauseRows = await db.Set<AccountPauseEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToListAsync(token);
        if (pauseRows.Any(r => r.ScopeCode != "Category" || r.CategoryId is null))
            throw new NotSupportedException("Effective pause history must use the domain's category union intervals.");
        var paused = await db.Set<CategoryProgressEntity>().Where(r => r.AccountId == account.Id && r.IsExplicitlyPaused).Select(r => r.CategoryId).ToListAsync(token);

        // The explicit category array is ordered by the successful pause/resume inputs. It is small
        // preference history only; loading it never replays quest, completion or reward commands.
        var pauseInputs = await db.Set<QuestCommandHistoryEntity>().Where(r => r.AccountId == account.Id && (r.ActionCode == "pause" || r.ActionCode == "resume") && r.CategoryId != null)
            .OrderBy(r => r.AccountMutationVersion).Select(r => new { r.ActionCode, r.CategoryId }).ToListAsync(token);
        var pauseOrder = new List<string>();
        foreach (var input in pauseInputs)
        {
            if (input.ActionCode == "resume")
                pauseOrder.Remove(input.CategoryId!);
            else if (!pauseOrder.Contains(input.CategoryId!))
                pauseOrder.Add(input.CategoryId!);
        }

        if (!pauseOrder.Order().SequenceEqual(paused.Order()))
            throw new InvalidOperationException("Explicit pause projection differs from retained preference history.");
        var schedule = new ScheduleState(series, [.. pauseRows.Select(r => new PauseWindow(r.CategoryId!, r.StartedAt, r.EndedAt))], [.. pauseOrder], account.IsAccountPaused);
        var occurrenceRows = await db.Set<QuestOccurrenceEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToListAsync(token);
        var occurrences = occurrenceRows.Select(r => CurrentOccurrence(r, terms, seriesRevisions)).ToArray();
        var occurrenceRevisions = await db.Set<QuestOccurrenceRevisionEntity>().Where(r => r.AccountId == account.Id).ToDictionaryAsync(r => r.Id, token);
        var events = await db.Set<QuestOccurrenceEventEntity>().Where(r => r.AccountId == account.Id && (r.EventCode == "Completed" || r.EventCode == "Undone")).ToDictionaryAsync(r => r.Id, token);
        var completionRows = await db.Set<QuestCompletionEntity>().Where(r => r.AccountId == account.Id).ToListAsync(token);
        var completions = completionRows.OrderBy(r => events[r.Id].AccountMutationVersion).Select(r => new Completion(r.Id, r.QuestOccurrenceId, r.RecordedAt, terms[occurrenceRevisions[r.QuestOccurrenceRevisionId].QuestDefinitionRevisionId])).ToArray();
        var undos = completionRows.Where(r => r.UndoQuestOccurrenceEventId is not null).OrderBy(r => events[r.UndoQuestOccurrenceEventId!.Value].AccountMutationVersion)
            .Select(r => new UndoEvent(r.UndoQuestOccurrenceEventId!.Value, r.Id, r.UndoneAt!.Value)).ToArray();
        var currentDefinitions = definitions.Select(r => new QuestDefinition(terms[definitionRevisions.Single(v => v.QuestDefinitionId == r.Id && v.Revision == r.Revision).Id], r.Revision, r.ArchivedAt is not null));
        return new(account.TimeZoneId, occurrences, completions, undos, currentDefinitions, profile, schedule);

        string SkillKey(Guid id)
        {
            var skill = skills.Single(s => s.Id == id);
            return skill.ClientKey ?? id.ToString("D");
        }
    }

    private static Occurrence CurrentOccurrence(QuestOccurrenceEntity row, Dictionary<Guid, Quest> terms, Dictionary<Guid, QuestSeriesRevisionEntity> series) => new(
        row.Id,
        terms[row.QuestDefinitionRevisionId],
        DateText(row.DueOn),
        row.DeadlineAt is { } deadline ? LocalInstant(deadline, row.DeadlineTimeZoneId!) : null,
        (row.OriginatedAt ?? row.AcceptedAt!.Value).ToOffset(TimeSpan.FromMinutes(row.OriginatedOffsetMinutes)),
        TimeText(row.PlannedTime),
        row.ParentQuestOccurrenceId,
        new(
            row.QuestSeriesId,
            row.SeriesSequence,
            row.QuestSeriesRevisionId is { } revision ? series[revision].ScheduleVersion : null,
            row.FrozenAt,
            row.IsIndividuallyFrozen,
            row.AbandonedAt,
            row.LockedLoss is { } loss ? checked((int)loss) : null,
            row.LossCategoryId,
            row.AcceptedAt is null,
            row.SourceSyncAnchorId,
            row.DeadlineTimeZoneId));

    private static DateTimeOffset LocalInstant(DateTimeOffset instant, string zone) => NodaTime.Instant.FromDateTimeOffset(instant).InZone(Scheduling.Zone(zone)).ToDateTimeOffset();
}
