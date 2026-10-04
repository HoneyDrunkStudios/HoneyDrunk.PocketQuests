using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for QuestDefinition.</summary>
public interface IQuestDefinitionService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<QuestDefinitionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Stages a validated change while retaining immutable identity and history.</summary>
    /// <param name="accountId">Account resolved by the authenticated command boundary.</param>
    /// <param name="value">Proposed entity values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked entity to commit in the caller transaction.</returns>
    Task<QuestDefinitionEntity> SaveAsync(Guid accountId, QuestDefinitionEntity value, CancellationToken cancellationToken = default);

    /// <summary>Retains immutable quest terms and typed allocation history.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="quest">Frozen reward terms.</param>
    /// <param name="revision">Explicit custom-definition revision, when supplied.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task<Guid> EnsureTermsAsync(QuestCommit change, Quest quest, int? revision = null, CancellationToken token = default);

    /// <summary>Updates custom definition heads while retaining their immutable revisions.</summary>
    /// <param name="change">Validated state for the current transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the staged entity changes.</returns>
    Task ApplyDefinitionsAsync(QuestCommit change, CancellationToken token = default);

    /// <summary>Reads retained custom definition revisions for an account export.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The requested source-derived result.</returns>
    Task<IReadOnlyList<QuestDefinition>> ReadHistoryAsync(Guid accountId, CancellationToken token = default);
}
