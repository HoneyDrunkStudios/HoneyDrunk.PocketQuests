using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class OccurrenceMapping
{
    internal static QuestOccurrenceEntity ToEntity(QuestMutation change, OccurrenceView view, QuestDefinitionRevisionEntity term, QuestOccurrenceEntity? prior, Guid? seriesRevisionId, int creationOrdinal)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var occurrence = view.Occurrence;
        var life = occurrence.Lifecycle ?? new();
        var termId = term.Id;
        return new QuestOccurrenceEntity
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
            CreationOrdinal = creationOrdinal,
            FrozenAt = life.FrozenAt,
            IsIndividuallyFrozen = life.IndividuallyFrozen,
            AbandonedAt = life.AbandonedAt,
            LockedLoss = life.LockedLoss,
            LossCategoryId = life.LossCategoryId,
            Revision = prior?.Revision ?? 1,
            QuestSeriesId = life.SeriesId,
            QuestSeriesRevisionId = seriesRevisionId,
            SeriesSequence = life.Sequence,
            ParentQuestOccurrenceId = occurrence.ParentId,
            SourceSyncAnchorId = life.SourceAnchorId,
        };
    }

    internal static QuestOccurrenceRevisionEntity ToRevision(QuestMutation change, QuestOccurrenceEntity row, Guid revisionId)
    {
        var account = change.Account;
        var recordedAt = change.RecordedAt;
        return new QuestOccurrenceRevisionEntity
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
        };
    }

    internal static QuestOccurrenceEventEntity ToEvent(QuestMutation change, Guid id, Guid occurrenceId, int revision, string code, DateTimeOffset at) => new()
    {
        Id = id,
        AccountId = change.Account.Id,
        QuestOccurrenceId = occurrenceId,
        QuestOccurrenceRevisionId = QuestValues.Derived(change.Account.Id, $"occurrence/{occurrenceId:D}/revision/{revision}"),
        EventCode = code,
        EffectiveAt = at,
        AccountMutationVersion = change.Version,
        CommandReceiptId = change.ReceiptId,
    };

    internal static void ApplyTo(this QuestOccurrenceEntity source, QuestOccurrenceEntity target)
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
    }
}
