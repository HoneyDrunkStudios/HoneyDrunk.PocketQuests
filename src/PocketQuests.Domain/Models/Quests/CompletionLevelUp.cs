namespace PocketQuests.Domain.Models.Quests;

/// <summary>A level change caused by one confirmed completion transaction.</summary>
/// <param name="Track">The progression track associated with this operation.</param>
/// <param name="TrackId">The progression track identifier.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="From">The beginning of the represented range or transition.</param>
/// <param name="To">The end of the represented range or transition.</param>
public record CompletionLevelUp(string Track, string TrackId, string Name, int From, int To);
