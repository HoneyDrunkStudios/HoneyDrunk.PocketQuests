namespace PocketQuests.Domain.Models.Progress;

/// <summary>The current category streak; Rate is an integer bonus percentage.</summary>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Days">The number of days involved in this schedule or pause.</param>
/// <param name="QualifiedToday">Whether the account qualified on the current local date.</param>
/// <param name="Rate">The rate used by this calculation.</param>
public record Streak(string CategoryId, int Days, bool QualifiedToday, int Rate);
