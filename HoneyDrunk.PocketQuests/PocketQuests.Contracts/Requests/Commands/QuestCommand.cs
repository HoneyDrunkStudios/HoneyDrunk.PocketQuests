using PocketQuests.Contracts.Enums.Profiles;
using PocketQuests.Contracts.Enums.Schedules;
using PocketQuests.Contracts.Requests.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Requests.Commands;

/// <summary>The public QuestCommand JSON contract, independent of storage and domain behavior.</summary>
public sealed record QuestCommand(Guid OperationId, string Action, Guid? OccurrenceId = null,
    string? QuestId = null, string? DueDate = null, Guid? CompletionId = null,
    QuestInput? Definition = null, int? ExpectedRevision = null, string? SkillId = null, Experience? Experience = null,
    ImmutableArray<string>? Interests = null, string? PlannedTime = null, Guid? ParentId = null, string? RewardId = null, Guid? SeriesId = null, Cadence? Cadence = null, int? Interval = null,
    string? CategoryId = null, bool ConfirmPenalty = false, int? AcceptedLoss = null, RecordedActionTime? RecordedTime = null, string? SkillName = null, QuestInput? AcceptedQuest = null, string? NewZone = null, string? ExpectedZone = null, bool ConfirmZoneChange = false, bool? ExpiryWarnings = null);
