using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestSeries ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questDefinitionService">QuestDefinition business behavior.</param>
/// <param name="questDefinitionRevisionService">QuestDefinitionRevision business behavior.</param>
/// <param name="questSeriesRevisionService">QuestSeriesRevision business behavior.</param>
public sealed class QuestSeriesService(IQuestSeriesDataService data, IQuestDefinitionService questDefinitionService, IQuestDefinitionRevisionService questDefinitionRevisionService, IQuestSeriesRevisionService questSeriesRevisionService) : IQuestSeriesService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestSeriesEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestSeriesEntity> SaveAsync(Guid accountId, QuestSeriesEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var configuration = (await questSeriesRevisionService.GetByAccountIdAsync(accountId, cancellationToken))
            .SingleOrDefault(row => row.QuestSeriesId == value.Id && row.Revision == value.Revision)
            ?? throw new InvalidOperationException("A series requires its owned immutable configuration.");
        var terms = (await questDefinitionRevisionService.GetByAccountIdAsync(accountId, cancellationToken))
            .SingleOrDefault(row => row.Id == configuration.QuestDefinitionRevisionId);
        if (terms is null || terms.QuestDefinitionId != value.QuestDefinitionId)
            throw new InvalidOperationException("The series definition must match its immutable configuration.");
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (value.Revision < original.Revision || value.Revision > original.Revision + 1
            || (value.QuestDefinitionId != original.QuestDefinitionId && value.Revision == original.Revision))
            throw new InvalidOperationException("A configuration change requires the next owned series revision.");
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.AccountId != current.AccountId
            || original.CreationOrdinal != current.CreationOrdinal)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.CreationOrdinal != value.CreationOrdinal)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.QuestDefinitionId = value.QuestDefinitionId;
        current.Revision = value.Revision;
        current.NextSequence = value.NextSequence;
        current.PauseDays = value.PauseDays;
        current.NextDeliveryOn = value.NextDeliveryOn;
        current.StoppedAt = value.StoppedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplySeriesAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        foreach (var series in aggregate.Schedule.Series)
        {
            var termId = await questDefinitionService.EnsureTermsAsync(change, series.Quest, token: token);
            var prior = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(s => s.Id == series.Id);
            var oldRevision = prior is null ? null : (await questSeriesRevisionService.GetByAccountIdAsync(account.Id, token)).Single(r => r.QuestSeriesId == series.Id && r.Revision == prior.Revision);
            var revision = new QuestSeriesRevisionEntity
            {
                Id = oldRevision?.Id ?? Guid.NewGuid(),
                AccountId = account.Id,
                QuestSeriesId = series.Id,
                Revision = prior?.Revision ?? 1,
                QuestDefinitionRevisionId = termId,
                CadenceCode = series.Cadence.ToString(),
                Interval = series.Interval,
                AnchorOn = QuestValues.Date(series.Anchor)!.Value,
                PlannedTime = QuestValues.Time(series.PlannedTime),
                HasAutoAcceptPenalty = series.AutoAcceptPenalty,
                EffectiveAt = series.EffectiveAt ?? recordedAt,
                ScheduleVersion = series.Version,
                CommandReceiptId = change.IsInternal ? oldRevision?.CommandReceiptId ?? throw new InvalidOperationException("An internal transition cannot introduce new series configuration.") : command.OperationId,
                CreatedAt = now
            };
            if (oldRevision is null || !SameConfiguration(oldRevision, revision))
            {
                revision.Revision = (prior?.Revision ?? 0) + 1;
                revision.Id = QuestValues.Derived(account.Id, $"series/{series.Id:D}/revision/{revision.Revision}");
                await questSeriesRevisionService.SaveAsync(account.Id, revision, token);
            }
            else
            {
                revision = oldRevision;
            }

            DateOnly? next = null;
            if (!series.Stopped)
            {
                try
                {
                    var date = Scheduling.Recurrence(Scheduling.ParseDate(series.Anchor), series.Cadence, series.Interval, series.NextSequence, series.PauseDays);
                    if (date.Year < 9999)
                        next = QuestValues.Date(Scheduling.DateText(date));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // End-of-calendar recurrence has no further delivery date.
                }
            }

            await SaveAsync(
                account.Id,
                new QuestSeriesEntity
                {
                    Id = series.Id,
                    AccountId = account.Id,
                    QuestDefinitionId = (await questDefinitionRevisionService.GetByAccountIdAsync(account.Id, token)).Single(r => r.Id == termId).QuestDefinitionId,
                    Revision = revision.Revision,
                    NextSequence = series.NextSequence,
                    CreationOrdinal = aggregate.Schedule.Series.IndexOf(series) + 1,
                    PauseDays = series.PauseDays,
                    NextDeliveryOn = next,
                    StoppedAt = series.Stopped ? prior?.StoppedAt ?? recordedAt : null,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
        }
    }

    private static bool SameConfiguration(QuestSeriesRevisionEntity first, QuestSeriesRevisionEntity second) =>
        first.Id == second.Id
        && first.AccountId == second.AccountId
        && first.QuestSeriesId == second.QuestSeriesId
        && first.Revision == second.Revision
        && first.QuestDefinitionRevisionId == second.QuestDefinitionRevisionId
        && first.CadenceCode == second.CadenceCode
        && first.Interval == second.Interval
        && first.AnchorOn == second.AnchorOn
        && first.PlannedTime == second.PlannedTime
        && first.HasAutoAcceptPenalty == second.HasAutoAcceptPenalty
        && first.EffectiveAt == second.EffectiveAt
        && first.ScheduleVersion == second.ScheduleVersion;
}
