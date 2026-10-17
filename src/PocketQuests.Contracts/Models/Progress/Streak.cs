namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public Streak JSON contract, independent of storage and domain behavior.</summary>
public sealed record Streak(string CategoryId, int Days, bool QualifiedToday, int Rate);
