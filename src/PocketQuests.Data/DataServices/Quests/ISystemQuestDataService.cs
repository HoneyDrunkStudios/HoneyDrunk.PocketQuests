using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Administration;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Persistence operations and entity-specific queries for SystemQuest.</summary>
public interface ISystemQuestDataService : IBaseDataService<SystemQuestEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<SystemQuestEntity>> GetCatalog(CancellationToken cancellationToken = default);

    /// <summary>Checks existence without tracking a catalog head.</summary>
    /// <param name="id">Stable system quest code.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Whether the quest exists.</returns>
    Task<bool> Exists(string id, CancellationToken token);

    /// <summary>Reads the tracked catalog head for a serialized edit.</summary>
    /// <param name="id">The stable system quest code.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current head, if present.</returns>
    Task<SystemQuestEntity?> Editable(string id, CancellationToken token);

    /// <summary>Reads a bounded authored catalog with draft and published terms in one SQL query.</summary>
    /// <param name="limit">The bounded lookahead limit.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Selected templates in stable identity order.</returns>
    Task<IReadOnlyList<SystemQuestStudioRow>> Studio(int limit, CancellationToken token);

    /// <summary>Reads only published immutable content in one bounded query.</summary>
    /// <param name="limit">The lookahead bound.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Public revisions in stable identity order.</returns>
    Task<IReadOnlyList<SystemQuestRevisionEntity>> Published(int limit, CancellationToken token);

    /// <summary>Reads one currently published template for a new acceptance.</summary>
    /// <param name="id">Stable system quest identity.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The published immutable revision, if available.</returns>
    Task<SystemQuestRevisionEntity?> ReadPublished(string id, CancellationToken token);

    /// <summary>Stages a new head and revision, then connects the initial draft pointer inside the caller transaction.</summary>
    /// <param name="head">The new stable identity.</param>
    /// <param name="revision">The first immutable revision.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion after the initial circular dependency is inserted, before transaction commit.</returns>
    Task AddInitialDraft(SystemQuestEntity head, SystemQuestRevisionEntity revision, CancellationToken token);
}
