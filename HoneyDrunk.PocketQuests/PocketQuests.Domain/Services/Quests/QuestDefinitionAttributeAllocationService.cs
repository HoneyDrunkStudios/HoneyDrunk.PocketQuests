using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestDefinitionAttributeAllocation ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class QuestDefinitionAttributeAllocationService(IQuestDefinitionAttributeAllocationDataService data) : IQuestDefinitionAttributeAllocationService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestDefinitionAttributeAllocationEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestDefinitionAttributeAllocationEntity> SaveAsync(Guid accountId, QuestDefinitionAttributeAllocationEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(new object[] { value.AccountId, value.QuestDefinitionRevisionId, value.AttributeId }, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.AccountId != current.AccountId
            || original.QuestDefinitionRevisionId != current.QuestDefinitionRevisionId
            || original.AttributeId != current.AttributeId
            || original.BasisPoints != current.BasisPoints
            || original.Position != current.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.AccountId != value.AccountId
            || current.QuestDefinitionRevisionId != value.QuestDefinitionRevisionId
            || current.AttributeId != value.AttributeId
            || current.BasisPoints != value.BasisPoints
            || current.Position != value.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }
}
