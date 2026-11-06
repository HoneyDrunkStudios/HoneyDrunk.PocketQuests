using PocketQuests.Contracts.Enums.Profiles;
using PocketQuests.Contracts.Enums.Schedules;
using PocketQuests.Contracts.Requests.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Requests.Commands;

/// <summary>The public QuestCommand JSON contract, independent of storage and domain behavior.</summary>
/// <param name="OperationId">The operation identifier used for idempotency.</param>
/// <param name="Action">The requested command action.</param>
/// <param name="OccurrenceId">The occurrence identifier.</param>
/// <param name="QuestId">The quest identifier.</param>
/// <param name="DueDate">The due date interpreted in the applicable account time zone.</param>
/// <param name="CompletionId">The completion identifier.</param>
/// <param name="Definition">The quest definition associated with the operation.</param>
/// <param name="ExpectedRevision">The revision required by optimistic concurrency validation.</param>
/// <param name="SkillId">The skill identifier.</param>
/// <param name="Experience">The experience represented by this result.</param>
/// <param name="Interests">The account interests included in this snapshot.</param>
/// <param name="PlannedTime">The planned occurrence time.</param>
/// <param name="ParentId">The parent resource identifier, when present.</param>
/// <param name="RewardId">The reward identifier.</param>
/// <param name="SeriesId">The recurring quest series identifier.</param>
/// <param name="Cadence">The recurrence cadence.</param>
/// <param name="Interval">The number of cadence units between recurrences.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="ConfirmPenalty">Whether the caller confirms the displayed penalty.</param>
/// <param name="AcceptedLoss">The loss fixed in the accepted quest terms.</param>
/// <param name="RecordedTime">The recorded-clock evidence submitted for the operation.</param>
/// <param name="SkillName">The human-readable skill name.</param>
/// <param name="AcceptedQuest">The immutable quest terms accepted for this occurrence.</param>
/// <param name="NewZone">The requested new account time zone.</param>
/// <param name="ExpectedZone">The account time zone the caller expects.</param>
/// <param name="ConfirmZoneChange">Whether the caller confirms the time-zone change.</param>
/// <param name="ExpiryWarnings">The warnings for expiring scheduled occurrences.</param>
/// <param name="CharacterName">The account character display name.</param>
/// <param name="PortraitId">The selected character portrait identifier, when present.</param>
/// <param name="AvatarCode">The selected character avatar code, when present.</param>
public sealed record QuestCommand(
    Guid OperationId,
    string Action,
    Guid? OccurrenceId = null,
    string? QuestId = null,
    string? DueDate = null,
    Guid? CompletionId = null,
    QuestInput? Definition = null,
    int? ExpectedRevision = null,
    string? SkillId = null,
    Experience? Experience = null,
    ImmutableArray<string>? Interests = null,
    string? PlannedTime = null,
    Guid? ParentId = null,
    string? RewardId = null,
    Guid? SeriesId = null,
    Cadence? Cadence = null,
    int? Interval = null,
    string? CategoryId = null,
    bool ConfirmPenalty = false,
    int? AcceptedLoss = null,
    RecordedActionTime? RecordedTime = null,
    string? SkillName = null,
    QuestInput? AcceptedQuest = null,
    string? NewZone = null,
    string? ExpectedZone = null,
    bool ConfirmZoneChange = false,
    bool? ExpiryWarnings = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CharacterName = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? PortraitId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? AvatarCode = null);
