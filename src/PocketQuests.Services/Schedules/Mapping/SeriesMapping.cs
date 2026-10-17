using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Services.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Schedules.Mapping;

internal static class SeriesMapping
{
    internal static QuestSeriesRevisionEntity ToRevision(this QuestSeries series, QuestMutation change, Guid termsId, int revision, Guid receiptId) => new()
    {
        Id = QuestValues.Derived(change.Account.Id, $"series/{series.Id:D}/revision/{revision}"),
        AccountId = change.Account.Id,
        QuestSeriesId = series.Id,
        Revision = revision,
        QuestDefinitionRevisionId = termsId,
        CadenceCode = series.Cadence.ToString(),
        Interval = series.Interval,
        AnchorOn = QuestValues.Date(series.Anchor)!.Value,
        PlannedTime = QuestValues.Time(series.PlannedTime),
        HasAutoAcceptPenalty = series.AutoAcceptPenalty,
        EffectiveAt = series.EffectiveAt ?? change.RecordedAt,
        ScheduleVersion = series.Version,
        CommandReceiptId = receiptId,
    };

    internal static QuestSeriesEntity ToHead(this QuestSeries series, QuestMutation change, Guid definitionId, int revision, int ordinal, DateOnly? next) => new()
    {
        Id = series.Id,
        AccountId = change.Account.Id,
        QuestDefinitionId = definitionId,
        Revision = revision,
        NextSequence = series.NextSequence,
        CreationOrdinal = ordinal,
        PauseDays = series.PauseDays,
        NextDeliveryOn = next,
    };

    internal static void ApplyTo(this QuestSeries series, QuestSeriesEntity target, Guid definitionId, int revision, DateOnly? next)
    {
        target.QuestDefinitionId = definitionId;
        target.Revision = revision;
        target.NextSequence = series.NextSequence;
        target.PauseDays = series.PauseDays;
        target.NextDeliveryOn = next;
    }
}
