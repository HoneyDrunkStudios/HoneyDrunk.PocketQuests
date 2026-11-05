namespace PocketQuests.Contracts.Models.Discoveries;

/// <summary>A private saved place and self-reported exploration status; no visit proof or reward.</summary>
public sealed record SavedPlace(Guid Id, string PlaceKey, string Name, string Kind, string CategoryId, decimal Latitude, decimal Longitude, string Notes, DateTimeOffset? VisitedAt, Guid? QuestOccurrenceId, int Revision, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
