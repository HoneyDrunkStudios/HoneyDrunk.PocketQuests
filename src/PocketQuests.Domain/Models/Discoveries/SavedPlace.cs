namespace PocketQuests.Domain.Models.Discoveries;

/// <summary>A private saved place and self-reported exploration status; no visit proof or reward.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="PlaceKey">The stable saved-place key.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Kind">The event or requirement classification.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Latitude">The place latitude in degrees.</param>
/// <param name="Longitude">The place longitude in degrees.</param>
/// <param name="Notes">The caller-supplied notes.</param>
/// <param name="VisitedAt">The recorded visit timestamp, when present.</param>
/// <param name="QuestOccurrenceId">The linked quest occurrence identifier, when present.</param>
/// <param name="Revision">The immutable resource revision.</param>
/// <param name="CreatedAt">The resource creation timestamp.</param>
/// <param name="ModifiedAt">The most recent modification timestamp.</param>
public sealed record SavedPlace(Guid Id, string PlaceKey, string Name, string Kind, string CategoryId, decimal Latitude, decimal Longitude, string Notes, DateTimeOffset? VisitedAt, Guid? QuestOccurrenceId, int Revision, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
