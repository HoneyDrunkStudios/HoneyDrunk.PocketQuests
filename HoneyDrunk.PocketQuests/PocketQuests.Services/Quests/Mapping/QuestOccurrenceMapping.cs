using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Schedules;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestOccurrenceMapping
{
    internal static OccurrenceView ToView(this Occurrence occurrence, QuestStatus status, Completion? completion, bool canUndo, PlannedMoment? planned) =>
        new(occurrence, status, completion, canUndo, planned);

    internal static QuestOccurrencePage ToPage(this IEnumerable<OccurrenceView> views, int? next, AccountEntity account) =>
        new([.. views], next, account.MutationVersion, account.ProjectionAsOfAt, account.HasPendingReconciliation);

    internal static Occurrence ToModel(this QuestOccurrenceEntity row, IReadOnlyDictionary<Guid, Quest> terms, IReadOnlyDictionary<Guid, QuestSeriesRevisionEntity> series) => new(
        row.Id,
        terms[row.QuestDefinitionRevisionId],
        QuestValues.DateText(row.DueOn),
        row.DeadlineAt is { } deadline ? NodaTime.Instant.FromDateTimeOffset(deadline).InZone(Scheduling.Zone(row.DeadlineTimeZoneId!)).ToDateTimeOffset() : null,
        (row.OriginatedAt ?? row.AcceptedAt!.Value).ToOffset(TimeSpan.FromMinutes(row.OriginatedOffsetMinutes)),
        QuestValues.TimeText(row.PlannedTime),
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

    internal static Occurrence ToModel(this QuestOccurrenceRevisionEntity row, IReadOnlyDictionary<Guid, Quest> terms, Occurrence current) =>
        current with
        {
            Quest = terms[row.QuestDefinitionRevisionId],
            DueDate = QuestValues.DateText(row.DueOn),
            PlannedTime = QuestValues.TimeText(row.PlannedTime),
            Deadline = row.DeadlineAt is { } deadline ? NodaTime.Instant.FromDateTimeOffset(deadline).InZone(Scheduling.Zone(row.DeadlineTimeZoneId!)).ToDateTimeOffset() : null,
            Lifecycle = (current.Lifecycle ?? new()) with
            {
                FrozenAt = row.FrozenAt,
                IndividuallyFrozen = row.IsIndividuallyFrozen,
                AbandonedAt = row.AbandonedAt,
                LockedLoss = row.LockedLoss is { } loss ? checked((int)loss) : null,
                LossCategoryId = row.LossCategoryId,
                Unaccepted = row.AcceptedAt is null,
                DeadlineZone = row.DeadlineTimeZoneId,
            },
        };
}
