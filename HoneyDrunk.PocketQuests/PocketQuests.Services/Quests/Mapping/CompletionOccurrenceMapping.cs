using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Services.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionOccurrenceMapping
{
    internal static void ApplyTo(this QuestCommit change, QuestCompletionRows rows, IReadOnlyDictionary<Guid, Quest> terms, QuestCompletionChanges changes)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var now = change.Now;
        var termIndex = rows.DefinitionRevisions.OrderByDescending(row => row.Revision)
            .GroupBy(row => JsonSerializer.Serialize(terms[row.Id])).ToDictionary(group => group.Key, group => group.First());
        var existing = rows.Occurrences.ToDictionary(row => row.Id);
        var configurations = rows.SeriesRevisions.GroupBy(row => (first: row.QuestSeriesId, second: row.ScheduleVersion))
            .ToDictionary(group => group.Key, group => group.MaxBy(row => row.Revision)!);
        foreach (var view in change.State.Occurrences)
        {
            var occurrence = view.Occurrence;
            var life = occurrence.Lifecycle ?? new();
            var term = termIndex[JsonSerializer.Serialize(occurrence.Quest)];
            var termId = term.Id;
            var prior = existing.GetValueOrDefault(occurrence.Id);
            var seriesRevision = life.SeriesId is { } seriesId && life.ScheduleVersion is { } scheduleVersion ? configurations[(seriesId, scheduleVersion)] : null;
            var row = new QuestOccurrenceEntity
            {
                Id = occurrence.Id,
                AccountId = account.Id,
                QuestDefinitionId = term.QuestDefinitionId,
                QuestDefinitionRevisionId = termId,
                CategoryId = occurrence.Quest.CategoryId,
                DueOn = QuestValues.Date(occurrence.DueDate),
                PlannedTime = QuestValues.Time(occurrence.PlannedTime),
                DeadlineAt = occurrence.Deadline,
                DeadlineTimeZoneId = life.DeadlineZone ?? aggregate.Zone,
                StateCode = view.Status.ToString(),
                AcceptedAt = life.Unaccepted ? null : occurrence.AcceptedAt,
                OriginatedAt = occurrence.AcceptedAt,
                OriginatedOffsetMinutes = checked((short)occurrence.AcceptedAt.Offset.TotalMinutes),
                CreationOrdinal = aggregate.Occurrences.IndexOf(occurrence) + 1,
                FrozenAt = life.FrozenAt,
                IsIndividuallyFrozen = life.IndividuallyFrozen,
                AbandonedAt = life.AbandonedAt,
                LockedLoss = life.LockedLoss,
                LossCategoryId = life.LossCategoryId,
                Revision = prior?.Revision ?? 1,
                QuestSeriesId = life.SeriesId,
                QuestSeriesRevisionId = prior?.QuestSeriesRevisionId ?? seriesRevision?.Id,
                SeriesSequence = life.Sequence,
                ParentQuestOccurrenceId = occurrence.ParentId,
                SourceSyncAnchorId = life.SourceAnchorId,
                CreatedAt = prior?.CreatedAt ?? now,
                ModifiedAt = now
            };
            if (prior is not null && SameOccurrence(prior, row))
                continue;
            row.Revision = (prior?.Revision ?? 0) + 1;
            var code = prior is null ? life.Unaccepted ? "Offered" : "Accepted"
                : life.AbandonedAt != prior.AbandonedAt ? "Abandoned"
                : life.FrozenAt is not null && life.FrozenAt != prior.FrozenAt ? "Frozen"
                : prior.FrozenAt is not null && life.FrozenAt is null ? "Resumed" : "Edited";
            var at = prior is null ? occurrence.AcceptedAt : code == "Abandoned" ? life.AbandonedAt!.Value : code == "Frozen" ? life.FrozenAt!.Value : recordedAt;
            if (prior is null)
                changes.Occurrences.Add(row);
            else
                row.ApplyTo(prior);
            var revisionId = QuestValues.Derived(account.Id, $"occurrence/{row.Id:D}/revision/{row.Revision}");
            changes.Revisions.Add(new QuestOccurrenceRevisionEntity
                {
                    Id = revisionId,
                    AccountId = account.Id,
                    QuestOccurrenceId = row.Id,
                    Revision = row.Revision,
                    QuestDefinitionId = row.QuestDefinitionId,
                    QuestDefinitionRevisionId = row.QuestDefinitionRevisionId,
                    CategoryId = row.CategoryId,
                    DueOn = row.DueOn,
                    PlannedTime = row.PlannedTime,
                    DeadlineAt = row.DeadlineAt,
                    DeadlineTimeZoneId = row.DeadlineTimeZoneId,
                    StateCode = row.StateCode,
                    AcceptedAt = row.AcceptedAt,
                    OriginatedAt = row.OriginatedAt,
                    OriginatedOffsetMinutes = row.OriginatedOffsetMinutes,
                    FrozenAt = row.FrozenAt,
                    IsIndividuallyFrozen = row.IsIndividuallyFrozen,
                    AbandonedAt = row.AbandonedAt,
                    LockedLoss = row.LockedLoss,
                    LossCategoryId = row.LossCategoryId,
                    AccountMutationVersion = change.Version,
                    EffectiveAt = recordedAt,
                    CommandReceiptId = change.ReceiptId,
                    CreatedAt = now
                });
            changes.Events.Add(ToEvent(change, QuestValues.Derived(account.Id, $"transition/{command.OperationId:D}/{row.Id:D}"), row.Id, row.Revision, code, at));
        }
    }

    internal static QuestOccurrenceEventEntity ToEvent(QuestCommit change, Guid id, Guid occurrenceId, int revision, string code, DateTimeOffset at) => new()
    {
        Id = id,
        AccountId = change.Account.Id,
        QuestOccurrenceId = occurrenceId,
        QuestOccurrenceRevisionId = QuestValues.Derived(change.Account.Id, $"occurrence/{occurrenceId:D}/revision/{revision}"),
        EventCode = code,
        EffectiveAt = at,
        AccountMutationVersion = change.Version,
        CommandReceiptId = change.ReceiptId,
        CreatedAt = change.Now,
    };

    private static void ApplyTo(this QuestOccurrenceEntity source, QuestOccurrenceEntity target)
    {
        target.QuestDefinitionRevisionId = source.QuestDefinitionRevisionId;
        target.CategoryId = source.CategoryId;
        target.DueOn = source.DueOn;
        target.PlannedTime = source.PlannedTime;
        target.DeadlineAt = source.DeadlineAt;
        target.DeadlineTimeZoneId = source.DeadlineTimeZoneId;
        target.StateCode = source.StateCode;
        target.AcceptedAt = source.AcceptedAt;
        target.FrozenAt = source.FrozenAt;
        target.IsIndividuallyFrozen = source.IsIndividuallyFrozen;
        target.AbandonedAt = source.AbandonedAt;
        target.LockedLoss = source.LockedLoss;
        target.LossCategoryId = source.LossCategoryId;
        target.Revision = source.Revision;
        target.ParentQuestOccurrenceId = source.ParentQuestOccurrenceId;
        target.ModifiedAt = source.ModifiedAt > target.ModifiedAt ? source.ModifiedAt : target.ModifiedAt;
    }

    private static bool SameOccurrence(QuestOccurrenceEntity first, QuestOccurrenceEntity second) =>
        first.Id == second.Id
        && first.AccountId == second.AccountId
        && first.QuestDefinitionId == second.QuestDefinitionId
        && first.QuestDefinitionRevisionId == second.QuestDefinitionRevisionId
        && first.CategoryId == second.CategoryId
        && first.DueOn == second.DueOn
        && first.PlannedTime == second.PlannedTime
        && first.DeadlineAt == second.DeadlineAt
        && first.DeadlineTimeZoneId == second.DeadlineTimeZoneId
        && first.StateCode == second.StateCode
        && first.AcceptedAt == second.AcceptedAt
        && first.FrozenAt == second.FrozenAt
        && first.IsIndividuallyFrozen == second.IsIndividuallyFrozen
        && first.AbandonedAt == second.AbandonedAt
        && first.LockedLoss == second.LockedLoss
        && first.LossCategoryId == second.LossCategoryId
        && first.Revision == second.Revision
        && first.QuestSeriesId == second.QuestSeriesId
        && first.QuestSeriesRevisionId == second.QuestSeriesRevisionId
        && first.SeriesSequence == second.SeriesSequence
        && first.ParentQuestOccurrenceId == second.ParentQuestOccurrenceId
        && first.SourceSyncAnchorId == second.SourceSyncAnchorId
        && first.OriginatedAt == second.OriginatedAt
        && first.CreationOrdinal == second.CreationOrdinal
        && first.OriginatedOffsetMinutes == second.OriginatedOffsetMinutes;
}
