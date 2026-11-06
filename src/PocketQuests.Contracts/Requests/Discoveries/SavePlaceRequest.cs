namespace PocketQuests.Contracts.Requests.Discoveries;

/// <summary>Explicit online discovery edit with a required optimistic revision; zero creates a new record.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="PlaceKey">The stable saved-place key.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Kind">The event or requirement classification.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Latitude">The place latitude in degrees.</param>
/// <param name="Longitude">The place longitude in degrees.</param>
/// <param name="Notes">The caller-supplied notes.</param>
/// <param name="Visited">Whether the saved place has been visited.</param>
/// <param name="QuestOccurrenceId">The linked quest occurrence identifier, when present.</param>
/// <param name="ExpectedRevision">The revision required by optimistic concurrency validation.</param>
public sealed record SavePlaceRequest(Guid Id, string PlaceKey, string Name, string Kind, string CategoryId, decimal Latitude, decimal Longitude, string Notes, bool Visited, Guid? QuestOccurrenceId, int ExpectedRevision);
