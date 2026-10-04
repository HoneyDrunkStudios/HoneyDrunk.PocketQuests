using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestOccurrenceRevision ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="occurrences">Occurrence heads.</param>
public sealed class QuestOccurrenceRevisionService(IQuestOccurrenceRevisionDataService data, IQuestOccurrenceDataService occurrences) : IQuestOccurrenceRevisionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestOccurrenceRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestOccurrenceRevisionEntity> SaveAsync(Guid accountId, QuestOccurrenceRevisionEntity value, CancellationToken cancellationToken = default)
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
            || original.QuestOccurrenceId != current.QuestOccurrenceId
            || original.Revision != current.Revision
            || original.QuestDefinitionId != current.QuestDefinitionId
            || original.QuestDefinitionRevisionId != current.QuestDefinitionRevisionId
            || original.CategoryId != current.CategoryId
            || original.DueOn != current.DueOn
            || original.PlannedTime != current.PlannedTime
            || original.DeadlineAt != current.DeadlineAt
            || original.DeadlineTimeZoneId != current.DeadlineTimeZoneId
            || original.StateCode != current.StateCode
            || original.AcceptedAt != current.AcceptedAt
            || original.FrozenAt != current.FrozenAt
            || original.IsIndividuallyFrozen != current.IsIndividuallyFrozen
            || original.AbandonedAt != current.AbandonedAt
            || original.LockedLoss != current.LockedLoss
            || original.LossCategoryId != current.LossCategoryId
            || original.AccountMutationVersion != current.AccountMutationVersion
            || original.EffectiveAt != current.EffectiveAt
            || original.CommandReceiptId != current.CommandReceiptId
            || original.OriginatedAt != current.OriginatedAt
            || original.OriginatedOffsetMinutes != current.OriginatedOffsetMinutes)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestOccurrenceId != value.QuestOccurrenceId
            || current.Revision != value.Revision
            || current.QuestDefinitionId != value.QuestDefinitionId
            || current.QuestDefinitionRevisionId != value.QuestDefinitionRevisionId
            || current.CategoryId != value.CategoryId
            || current.DueOn != value.DueOn
            || current.PlannedTime != value.PlannedTime
            || current.DeadlineAt != value.DeadlineAt
            || current.DeadlineTimeZoneId != value.DeadlineTimeZoneId
            || current.StateCode != value.StateCode
            || current.AcceptedAt != value.AcceptedAt
            || current.FrozenAt != value.FrozenAt
            || current.IsIndividuallyFrozen != value.IsIndividuallyFrozen
            || current.AbandonedAt != value.AbandonedAt
            || current.LockedLoss != value.LockedLoss
            || current.LossCategoryId != value.LossCategoryId
            || current.AccountMutationVersion != value.AccountMutationVersion
            || current.EffectiveAt != value.EffectiveAt
            || current.CommandReceiptId != value.CommandReceiptId
            || current.OriginatedAt != value.OriginatedAt
            || current.OriginatedOffsetMinutes != value.OriginatedOffsetMinutes)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task<Guid> CurrentRevisionIdAsync(Guid accountId, Guid occurrenceId, CancellationToken token = default)
    {
        var occurrence = await occurrences.FindByIdAsync(occurrenceId, token)
            ?? throw new KeyNotFoundException("Occurrence was not found.");
        if (occurrence.AccountId != accountId)
            throw new UnauthorizedAccessException("Occurrence belongs to another account.");
        return QuestValues.Derived(accountId, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}");
    }

    internal static Occurrence ToModel(QuestOccurrenceRevisionEntity row, Dictionary<Guid, Quest> terms, Occurrence current) =>
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
