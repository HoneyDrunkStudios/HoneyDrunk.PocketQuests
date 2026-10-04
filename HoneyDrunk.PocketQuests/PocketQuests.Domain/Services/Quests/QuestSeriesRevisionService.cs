using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestSeriesRevision ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class QuestSeriesRevisionService(IQuestSeriesRevisionDataService data) : IQuestSeriesRevisionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestSeriesRevisionEntity> SaveAsync(Guid accountId, QuestSeriesRevisionEntity value, CancellationToken cancellationToken = default)
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
            || original.QuestSeriesId != current.QuestSeriesId
            || original.Revision != current.Revision
            || original.QuestDefinitionRevisionId != current.QuestDefinitionRevisionId
            || original.CadenceCode != current.CadenceCode
            || original.Interval != current.Interval
            || original.AnchorOn != current.AnchorOn
            || original.PlannedTime != current.PlannedTime
            || original.HasAutoAcceptPenalty != current.HasAutoAcceptPenalty
            || original.EffectiveAt != current.EffectiveAt
            || original.CommandReceiptId != current.CommandReceiptId
            || original.ScheduleVersion != current.ScheduleVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestSeriesId != value.QuestSeriesId
            || current.Revision != value.Revision
            || current.QuestDefinitionRevisionId != value.QuestDefinitionRevisionId
            || current.CadenceCode != value.CadenceCode
            || current.Interval != value.Interval
            || current.AnchorOn != value.AnchorOn
            || current.PlannedTime != value.PlannedTime
            || current.HasAutoAcceptPenalty != value.HasAutoAcceptPenalty
            || current.EffectiveAt != value.EffectiveAt
            || current.CommandReceiptId != value.CommandReceiptId
            || current.ScheduleVersion != value.ScheduleVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<QuestSeriesRevisionEntity>> GetCommittedByAccountIdAsync(Guid accountId, CancellationToken token = default) =>
        data.GetCommittedByAccountIdAsync(accountId, token);
}
