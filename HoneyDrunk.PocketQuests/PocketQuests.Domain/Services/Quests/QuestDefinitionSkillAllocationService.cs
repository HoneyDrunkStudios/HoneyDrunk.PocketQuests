using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestDefinitionSkillAllocation ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class QuestDefinitionSkillAllocationService(IQuestDefinitionSkillAllocationDataService data) : IQuestDefinitionSkillAllocationService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestDefinitionSkillAllocationEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestDefinitionSkillAllocationEntity> SaveAsync(Guid accountId, QuestDefinitionSkillAllocationEntity value, CancellationToken cancellationToken = default)
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
            || original.QuestDefinitionRevisionId != current.QuestDefinitionRevisionId
            || original.SystemSkillId != current.SystemSkillId
            || original.CustomSkillId != current.CustomSkillId
            || original.BasisPoints != current.BasisPoints
            || original.Position != current.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestDefinitionRevisionId != value.QuestDefinitionRevisionId
            || current.SystemSkillId != value.SystemSkillId
            || current.CustomSkillId != value.CustomSkillId
            || current.BasisPoints != value.BasisPoints
            || current.Position != value.Position)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }
}
