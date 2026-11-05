namespace PocketQuests.Contracts.Requests.Discoveries;

/// <summary>Explicit online discovery edit with a required optimistic revision; zero creates a new record.</summary>
public sealed record SavePlaceRequest(Guid Id, string PlaceKey, string Name, string Kind, string CategoryId, decimal Latitude, decimal Longitude, string Notes, bool Visited, Guid? QuestOccurrenceId, int ExpectedRevision);
