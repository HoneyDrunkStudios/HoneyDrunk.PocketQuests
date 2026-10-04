using System.Collections.Immutable;

namespace PocketQuests.Api.Contracts.Quests;

/// <summary>Explicit mappings for the quests HTTP contracts.</summary>
public static class QuestsContractMapping
{
    /// <summary>Maps Quest explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Definitions.Quest ToContract(this PocketQuests.Domain.Models.Quests.Quest value) => new(
        value.Id,
        value.Title,
        value.Criterion,
        value.CategoryId,
        (PocketQuests.Api.Contracts.Progress.Rank)value.Rank,
        (PocketQuests.Api.Contracts.Quests.Definitions.Effort)value.Effort,
        value.Attributes.Select(item => item.ToContract()).ToImmutableArray(),
        value.Skills.Select(item => item.ToContract()).ToImmutableArray(),
        value.IsCustom,
        value.Description,
        value.PenaltyPercent,
        value.BaseXp);

    /// <summary>Maps QuestDefinition explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Definitions.QuestDefinition ToContract(this PocketQuests.Domain.Models.Quests.QuestDefinition value) => new(
        value.Quest.ToContract(),
        value.Revision,
        value.Archived);

    /// <summary>Maps Share explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Definitions.Share ToContract(this PocketQuests.Domain.Models.Quests.Share value) => new(
        value.Id,
        value.BasisPoints);

    /// <summary>Maps Completion explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Events.Completion ToContract(this PocketQuests.Domain.Models.Quests.Completion value) => new(
        value.Id,
        value.OccurrenceId,
        value.RecordedAt,
        value.Snapshot?.ToContract());

    /// <summary>Maps UndoEvent explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Events.UndoEvent ToContract(this PocketQuests.Domain.Models.Quests.UndoEvent value) => new(
        value.Id,
        value.CompletionId,
        value.RecordedAt);

    /// <summary>Maps Occurrence explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Occurrences.Occurrence ToContract(this PocketQuests.Domain.Models.Quests.Occurrence value) => new(
        value.Id,
        value.Quest.ToContract(),
        value.DueDate,
        value.Deadline,
        value.AcceptedAt,
        value.PlannedTime,
        value.ParentId,
        value.Lifecycle?.ToContract());

    /// <summary>Maps OccurrenceLifecycle explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Quests.Occurrences.OccurrenceLifecycle ToContract(this PocketQuests.Domain.Models.Quests.OccurrenceLifecycle value) => new(
        value.SeriesId,
        value.Sequence,
        value.ScheduleVersion,
        value.FrozenAt,
        value.IndividuallyFrozen,
        value.AbandonedAt,
        value.LockedLoss,
        value.LossCategoryId,
        value.Unaccepted,
        value.SourceAnchorId,
        value.DeadlineZone);
}
