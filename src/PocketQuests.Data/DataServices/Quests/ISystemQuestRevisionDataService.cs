using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Immutable system template content revisions.</summary>
public interface ISystemQuestRevisionDataService : IBaseDataService<SystemQuestRevisionEntity>
{
    /// <summary>Reads one frozen revision.</summary>
    /// <param name="id">The stable revision key.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The retained revision, if present.</returns>
    Task<SystemQuestRevisionEntity?> Read(Guid id, CancellationToken token);
}
