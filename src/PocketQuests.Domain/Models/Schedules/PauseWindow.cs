namespace PocketQuests.Domain.Models.Schedules;

/// <summary>An effective union of category/account pauses in the selected local timezone.</summary>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="StartedAt">The beginning timestamp.</param>
/// <param name="EndedAt">The end timestamp, when present.</param>
public record PauseWindow(string CategoryId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt = null);
