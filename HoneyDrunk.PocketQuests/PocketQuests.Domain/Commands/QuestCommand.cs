using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Schedules;
using PocketQuests.Domain.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Commands;

/// <summary>An account-scoped command with a client-generated idempotency identifier.</summary>
public record QuestCommand(Guid OperationId, string Action, Guid? OccurrenceId = null,
    string? QuestId = null, string? DueDate = null, Guid? CompletionId = null,
    Quest? Definition = null, int? ExpectedRevision = null, string? SkillId = null, Experience? Experience = null,
    ImmutableArray<string>? Interests = null, string? PlannedTime = null, Guid? ParentId = null, string? RewardId = null, Guid? SeriesId = null, Cadence? Cadence = null, int? Interval = null,
    string? CategoryId = null, bool ConfirmPenalty = false, int? AcceptedLoss = null, RecordedActionTime? RecordedTime = null, string? SkillName = null, Quest? AcceptedQuest = null, string? NewZone = null, string? ExpectedZone = null, bool ConfirmZoneChange = false, bool? ExpiryWarnings = null);
