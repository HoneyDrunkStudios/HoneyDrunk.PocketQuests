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

    /// <summary>Reads a bounded newest-first page using the existing quest/version key.</summary>
    /// <param name="questId">Stable quest identity.</param>
    /// <param name="before">Exclusive version cursor.</param>
    /// <param name="limit">Maximum rows including lookahead.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Frozen revisions in descending version order.</returns>
    Task<IReadOnlyList<SystemQuestRevisionEntity>> History(string questId, int before, int limit, CancellationToken token);
}
