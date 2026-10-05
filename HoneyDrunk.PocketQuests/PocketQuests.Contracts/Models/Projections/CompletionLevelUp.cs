namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public CompletionLevelUp JSON contract, independent of storage and domain behavior.</summary>
public sealed record CompletionLevelUp(string Track, string TrackId, string Name, int From, int To);
