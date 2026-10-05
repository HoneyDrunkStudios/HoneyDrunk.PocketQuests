using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for SystemQuest.</summary>
public interface ISystemQuestDataService : IBaseDataService<SystemQuestEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<SystemQuestEntity>> GetCatalog(CancellationToken cancellationToken = default);
}
