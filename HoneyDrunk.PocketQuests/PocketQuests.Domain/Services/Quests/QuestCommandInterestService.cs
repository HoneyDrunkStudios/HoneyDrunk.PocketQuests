using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestCommandInterest ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class QuestCommandInterestService(IQuestCommandInterestDataService data) : IQuestCommandInterestService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestCommandInterestEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestCommandInterestEntity> SaveAsync(Guid accountId, QuestCommandInterestEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(new object[] { value.AccountId, value.QuestCommandHistoryId, value.CategoryId }, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.AccountId != current.AccountId
            || original.QuestCommandHistoryId != current.QuestCommandHistoryId
            || original.CategoryId != current.CategoryId
            || original.Position != current.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.AccountId != value.AccountId
            || current.QuestCommandHistoryId != value.QuestCommandHistoryId
            || current.CategoryId != value.CategoryId
            || current.Position != value.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }
}
