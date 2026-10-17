namespace PocketQuests.Domain.Models.Quests;

/// <summary>A level change caused by one confirmed completion transaction.</summary>
public record CompletionLevelUp(string Track, string TrackId, string Name, int From, int To);
