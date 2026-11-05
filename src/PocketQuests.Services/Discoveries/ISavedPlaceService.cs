using PocketQuests.Contracts.Models.Discoveries;
using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Contracts.Responses.Discoveries;
using System.Collections.Immutable;

namespace PocketQuests.Services.Discoveries;

/// <summary>Private account discovery journal with explicit online edits.</summary>
public interface ISavedPlaceService
{
    /// <summary>Reads saved discoveries for the authenticated account.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>Saved discoveries.</returns>
    Task<ImmutableArray<SavedPlace>> Read(CancellationToken token = default);

    /// <summary>Saves one observed revision without awarding XP or changing quests.</summary>
    /// <param name="request">Conditional edit.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Confirmed result.</returns>
    Task<SavedPlace> Save(SavePlaceRequest request, CancellationToken token = default);

    /// <summary>Removes an observed revision; repeat deletion of an absent ID succeeds.</summary>
    /// <param name="request">Conditional edit.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Confirmed result.</returns>
    Task<RemovalResult> Remove(RemovePlaceRequest request, CancellationToken token = default);
}
