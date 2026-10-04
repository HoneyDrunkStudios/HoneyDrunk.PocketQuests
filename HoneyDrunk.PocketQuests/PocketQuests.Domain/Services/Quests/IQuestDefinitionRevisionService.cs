using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestDefinitionRevision.</summary>
public interface IQuestDefinitionRevisionService
{
    /// <summary>Reconstructs retained reward terms from immutable revisions and typed allocations.</summary>
    /// <param name="accountId">Resolved owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <param name="selected">Optional selected revision identifiers.</param>
    /// <returns>Frozen quest terms keyed by revision.</returns>
    Task<Dictionary<Guid, Quest>> ReadTermsAsync(Guid accountId, CancellationToken token = default, Guid[]? selected = null);

    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestDefinitionRevisionEntity> SaveAsync(Guid accountId, QuestDefinitionRevisionEntity value, CancellationToken cancellationToken = default);
}
