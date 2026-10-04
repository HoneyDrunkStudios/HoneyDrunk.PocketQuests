using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestOccurrence ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questDefinitionService">QuestDefinition business behavior.</param>
/// <param name="questDefinitionRevisionService">QuestDefinitionRevision business behavior.</param>
/// <param name="questSeriesRevisionService">QuestSeriesRevision business behavior.</param>
/// <param name="questOccurrenceRevisionService">QuestOccurrenceRevision business behavior.</param>
/// <param name="questOccurrenceEventService">QuestOccurrenceEvent business behavior.</param>
/// <param name="completionData">Scoped IQuestCompletionDataService query access.</param>
/// <param name="revisionData">Scoped IQuestOccurrenceRevisionDataService query access.</param>
/// <param name="seriesData">Scoped IQuestSeriesRevisionDataService query access.</param>
public sealed class QuestOccurrenceService(IQuestOccurrenceDataService data, IQuestDefinitionService questDefinitionService, IQuestDefinitionRevisionService questDefinitionRevisionService, IQuestSeriesRevisionService questSeriesRevisionService, IQuestOccurrenceRevisionService questOccurrenceRevisionService, IQuestOccurrenceEventService questOccurrenceEventService, IQuestCompletionDataService completionData, IQuestOccurrenceRevisionDataService revisionData, IQuestSeriesRevisionDataService seriesData) : IQuestOccurrenceService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestOccurrenceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestOccurrenceEntity> SaveAsync(Guid accountId, QuestOccurrenceEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.AccountId != current.AccountId
            || original.QuestDefinitionId != current.QuestDefinitionId
            || original.QuestSeriesId != current.QuestSeriesId
            || original.QuestSeriesRevisionId != current.QuestSeriesRevisionId
            || original.SeriesSequence != current.SeriesSequence
            || original.SourceSyncAnchorId != current.SourceSyncAnchorId
            || original.OriginatedAt != current.OriginatedAt
            || original.CreationOrdinal != current.CreationOrdinal
            || original.OriginatedOffsetMinutes != current.OriginatedOffsetMinutes)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestDefinitionId != value.QuestDefinitionId
            || current.QuestSeriesId != value.QuestSeriesId
            || current.QuestSeriesRevisionId != value.QuestSeriesRevisionId
            || current.SeriesSequence != value.SeriesSequence
            || current.SourceSyncAnchorId != value.SourceSyncAnchorId
            || current.OriginatedAt != value.OriginatedAt
            || current.CreationOrdinal != value.CreationOrdinal
            || current.OriginatedOffsetMinutes != value.OriginatedOffsetMinutes)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.QuestDefinitionRevisionId = value.QuestDefinitionRevisionId;
        current.CategoryId = value.CategoryId;
        current.DueOn = value.DueOn;
        current.PlannedTime = value.PlannedTime;
        current.DeadlineAt = value.DeadlineAt;
        current.DeadlineTimeZoneId = value.DeadlineTimeZoneId;
        current.StateCode = value.StateCode;
        current.AcceptedAt = value.AcceptedAt;
        current.FrozenAt = value.FrozenAt;
        current.IsIndividuallyFrozen = value.IsIndividuallyFrozen;
        current.AbandonedAt = value.AbandonedAt;
        current.LockedLoss = value.LockedLoss;
        current.LossCategoryId = value.LossCategoryId;
        current.Revision = value.Revision;
        current.ParentQuestOccurrenceId = value.ParentQuestOccurrenceId;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplyOccurrencesAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var state = change.State;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        var configurations = command.Action == QuestActions.SaveDefinition
            ? await questSeriesRevisionService.GetCommittedByAccountIdAsync(account.Id, token) : await questSeriesRevisionService.GetByAccountIdAsync(account.Id, token);
        foreach (var view in state.Occurrences)
        {
            var occurrence = view.Occurrence;
            var life = occurrence.Lifecycle ?? new();
            var termId = await questDefinitionService.EnsureTermsAsync(change, occurrence.Quest, token: token);
            var term = (await questDefinitionRevisionService.GetByAccountIdAsync(account.Id, token)).Single(r => r.Id == termId);
            var prior = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(o => o.Id == occurrence.Id);

            // Definition editing runs after pre-command delivery. Those newly materialized
            // occurrences originated under the previous configuration even if their active
            // terms are then edited. Later deliveries use the newest internal configuration.
            var seriesRevision = life.SeriesId is null ? null : configurations
                .Where(r => r.QuestSeriesId == life.SeriesId && r.ScheduleVersion == life.ScheduleVersion).OrderByDescending(r => r.Revision).First();
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
                CreatedAt = now,
                ModifiedAt = now
            };
            if (prior is not null && SameOccurrence(prior, row))
            {
                continue;
            }

            row.Revision = (prior?.Revision ?? 0) + 1;
            var code = prior is null ? life.Unaccepted ? "Offered" : "Accepted"
                : life.AbandonedAt != prior.AbandonedAt ? "Abandoned"
                : life.FrozenAt is not null && life.FrozenAt != prior.FrozenAt ? "Frozen"
                : prior.FrozenAt is not null && life.FrozenAt is null ? "Resumed" : "Edited";
            var at = prior is null ? occurrence.AcceptedAt : code == "Abandoned" ? life.AbandonedAt!.Value : code == "Frozen" ? life.FrozenAt!.Value : recordedAt;
            await SaveAsync(account.Id, row, token);
            var revisionId = QuestValues.Derived(account.Id, $"occurrence/{row.Id:D}/revision/{row.Revision}");

            await questOccurrenceRevisionService.SaveAsync(
                account.Id,
                new QuestOccurrenceRevisionEntity
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
                },
                token);

            await questOccurrenceEventService.AppendAsync(change, QuestValues.Derived(account.Id, $"transition/{command.OperationId:D}/{row.Id:D}"), row.Id, code, at, token);
        }
    }

    /// <inheritdoc />
    public async Task<QuestOccurrencePage> ReadPageAsync(AccountEntity account, int after, int size, DateTimeOffset projectionAt, CancellationToken token = default)
    {
        var rows = (await data.GetPageAsync(account.Id, after, size, token)).ToList();
        var next = rows.Count > size ? rows[size - 1].CreationOrdinal : (int?)null;
        rows = [.. rows.Take(size)];
        var ids = rows.Select(r => r.Id).ToArray();
        var completions = await completionData.GetCurrentForOccurrencesAsync(account.Id, ids, projectionAt, token);
        var completionRevisionIds = completions.Select(r => r.QuestOccurrenceRevisionId).ToArray();
        var completionRevisions = (await revisionData.GetSelectedAsync(account.Id, completionRevisionIds, token)).ToDictionary(row => row.Id);
        var termIds = rows.Select(r => r.QuestDefinitionRevisionId).Concat(completionRevisions.Values.Select(r => r.QuestDefinitionRevisionId)).Distinct().ToArray();
        var terms = await questDefinitionRevisionService.ReadTermsAsync(account.Id, token, termIds);
        var seriesIds = rows.Where(r => r.QuestSeriesRevisionId is not null).Select(r => r.QuestSeriesRevisionId!.Value).Distinct().ToArray();
        var series = (await seriesData.GetSelectedAsync(account.Id, seriesIds, token)).ToDictionary(row => row.Id);
        var views = rows.Select(row =>
        {
            var occurrence = ToModel(row, terms, series);
            var completionRow = completions.SingleOrDefault(c => c.QuestOccurrenceId == row.Id);
            var completion = completionRow is null ? null : new Completion(completionRow.Id, row.Id, completionRow.RecordedAt, terms[completionRevisions[completionRow.QuestOccurrenceRevisionId].QuestDefinitionRevisionId]);
            var status = row.AcceptedAt is null ? QuestStatus.Offered : completion is not null ? QuestStatus.Completed
                : row.AbandonedAt is not null ? QuestStatus.Abandoned : row.FrozenAt is not null ? QuestStatus.Frozen
                : row.DeadlineAt <= projectionAt ? QuestStatus.Missed : QuestStatus.Active;
            return new OccurrenceView(occurrence, status, completion, completion is not null && projectionAt >= completion.RecordedAt && projectionAt < completion.RecordedAt.AddHours(24), occurrence.DueDate is not null && occurrence.PlannedTime is not null ? Scheduling.Planned(occurrence.DueDate, occurrence.PlannedTime, row.DeadlineTimeZoneId!) : null);
        }).ToArray();
        return new([.. views], next, account.MutationVersion, account.ProjectionAsOfAt, account.HasPendingReconciliation);
    }

    internal static Occurrence ToModel(QuestOccurrenceEntity row, Dictionary<Guid, Quest> terms, Dictionary<Guid, QuestSeriesRevisionEntity> series) => new(
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
