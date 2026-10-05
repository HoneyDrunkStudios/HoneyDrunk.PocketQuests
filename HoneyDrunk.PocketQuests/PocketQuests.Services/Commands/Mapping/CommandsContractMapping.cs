using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Common.Mapping;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Schedules.Mapping;
using PocketQuests.Services.Synchronization.Mapping;

namespace PocketQuests.Services.Commands.Mapping;

/// <summary>Explicit mappings for the commands HTTP contracts.</summary>
public static class CommandsContractMapping
{
    /// <summary>Maps QuestCommand explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Requests.Commands.QuestCommand ToModel(this PocketQuests.Domain.Models.Quests.QuestCommand value) => new(
        value.OperationId,
        value.Action,
        value.OccurrenceId,
        value.QuestId,
        value.DueDate,
        value.CompletionId,
        value.Definition?.ToInput(),
        value.ExpectedRevision,
        value.SkillId,
        (PocketQuests.Contracts.Enums.Profiles.Experience?)value.Experience,
        value.Interests,
        value.PlannedTime,
        value.ParentId,
        value.RewardId,
        value.SeriesId,
        (PocketQuests.Contracts.Enums.Schedules.Cadence?)value.Cadence,
        value.Interval,
        value.CategoryId,
        value.ConfirmPenalty,
        value.AcceptedLoss,
        value.RecordedTime?.ToModel(),
        value.SkillName,
        value.AcceptedQuest?.ToInput(),
        value.NewZone,
        value.ExpectedZone,
        value.ConfirmZoneChange,
        value.ExpiryWarnings);

    /// <summary>Maps QuestCommand explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Domain.Models.Quests.QuestCommand ToModel(this PocketQuests.Contracts.Requests.Commands.QuestCommand value) => new(
        value.OperationId,
        value.Action,
        value.OccurrenceId,
        value.QuestId,
        value.DueDate,
        value.CompletionId,
        value.Definition?.ToModel(),
        value.ExpectedRevision,
        value.SkillId,
        (PocketQuests.Domain.Models.Skills.Experience?)value.Experience,
        value.Interests,
        value.PlannedTime,
        value.ParentId,
        value.RewardId,
        value.SeriesId,
        (PocketQuests.Domain.Models.Schedules.Cadence?)value.Cadence,
        value.Interval,
        value.CategoryId,
        value.ConfirmPenalty,
        value.AcceptedLoss,
        value.RecordedTime?.ToModel(),
        value.SkillName,
        value.AcceptedQuest?.ToModel(),
        value.NewZone,
        value.ExpectedZone,
        value.ConfirmZoneChange,
        value.ExpiryWarnings);
}
