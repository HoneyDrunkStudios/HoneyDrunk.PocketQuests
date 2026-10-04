using PocketQuests.Api.Contracts.Catalogs;
using PocketQuests.Api.Contracts.Commands;
using PocketQuests.Api.Contracts.Common;
using PocketQuests.Api.Contracts.Exports;
using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests;
using PocketQuests.Api.Contracts.Schedules;
using PocketQuests.Api.Contracts.Synchronization;

namespace PocketQuests.Api.Contracts.Commands;

/// <summary>Explicit mappings for the commands HTTP contracts.</summary>
public static class CommandsContractMapping
{
    /// <summary>Maps QuestCommand explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Commands.QuestCommand ToContract(this PocketQuests.Domain.Models.Quests.QuestCommand value) => new(
        value.OperationId,
        value.Action,
        value.OccurrenceId,
        value.QuestId,
        value.DueDate,
        value.CompletionId,
        value.Definition?.ToInput(),
        value.ExpectedRevision,
        value.SkillId,
        (PocketQuests.Api.Contracts.Profiles.Experience?)value.Experience,
        value.Interests,
        value.PlannedTime,
        value.ParentId,
        value.RewardId,
        value.SeriesId,
        (PocketQuests.Api.Contracts.Schedules.Cadence?)value.Cadence,
        value.Interval,
        value.CategoryId,
        value.ConfirmPenalty,
        value.AcceptedLoss,
        value.RecordedTime?.ToContract(),
        value.SkillName,
        value.AcceptedQuest?.ToInput(),
        value.NewZone,
        value.ExpectedZone,
        value.ConfirmZoneChange,
        value.ExpiryWarnings);

    /// <summary>Maps QuestCommand explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Domain.Models.Quests.QuestCommand ToDomain(this PocketQuests.Api.Contracts.Commands.QuestCommand value) => new(
        value.OperationId,
        value.Action,
        value.OccurrenceId,
        value.QuestId,
        value.DueDate,
        value.CompletionId,
        value.Definition?.ToDomain(),
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
        value.RecordedTime?.ToDomain(),
        value.SkillName,
        value.AcceptedQuest?.ToDomain(),
        value.NewZone,
        value.ExpectedZone,
        value.ConfirmZoneChange,
        value.ExpiryWarnings);
}
