namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public Streak JSON contract, independent of storage and domain behavior.</summary>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="Days">The number of days involved in this schedule or pause.</param>
/// <param name="QualifiedToday">Whether the account qualified on the current local date.</param>
/// <param name="Rate">The rate used by this calculation.</param>
public sealed record Streak(string CategoryId, int Days, bool QualifiedToday, int Rate);
