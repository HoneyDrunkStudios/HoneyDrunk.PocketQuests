using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Business access and invariants for SystemQuest.</summary>
public interface ISystemQuestService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<SystemQuestEntity>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
