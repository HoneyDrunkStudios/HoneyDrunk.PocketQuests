namespace PocketQuests.Domain.Models.Schedules;

/// <summary>An effective union of category/account pauses in the selected local timezone.</summary>
public record PauseWindow(string CategoryId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt = null);
