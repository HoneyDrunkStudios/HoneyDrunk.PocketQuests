using PocketQuests.Data.Entities.Skills;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Skills;

/// <summary>Business access and invariants for CustomSkill.</summary>
public interface ICustomSkillService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<CustomSkillEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<CustomSkillEntity> SaveAsync(Guid accountId, CustomSkillEntity value, CancellationToken cancellationToken = default);

    /// <summary>Stages this entity's owned profile history from the validated command.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplySkillsAsync(QuestCommit change, CancellationToken token = default);
}
