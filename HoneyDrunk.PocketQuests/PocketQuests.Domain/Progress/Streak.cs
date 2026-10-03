namespace PocketQuests.Domain.Progress;

/// <summary>The current category streak; Rate is an integer bonus percentage.</summary>
public record Streak(string CategoryId, int Days, bool QualifiedToday, int Rate);
