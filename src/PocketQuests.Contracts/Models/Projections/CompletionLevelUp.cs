namespace PocketQuests.Contracts.Models.Projections;

/// <summary>The public CompletionLevelUp JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Track">The progression track associated with this operation.</param>
/// <param name="TrackId">The progression track identifier.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="From">The beginning of the represented range or transition.</param>
/// <param name="To">The end of the represented range or transition.</param>
public sealed record CompletionLevelUp(string Track, string TrackId, string Name, int From, int To);
