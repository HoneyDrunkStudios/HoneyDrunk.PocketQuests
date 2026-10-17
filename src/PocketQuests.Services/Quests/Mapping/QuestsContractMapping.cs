using System.Collections.Immutable;

namespace PocketQuests.Services.Quests.Mapping;

/// <summary>Explicit mappings for the quests HTTP contracts.</summary>
public static class QuestsContractMapping
{
    /// <summary>Maps Quest explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.Quest ToModel(this PocketQuests.Domain.Models.Quests.Quest value) => new(
        value.Id,
        value.Title,
        value.Criterion,
        value.CategoryId,
        (PocketQuests.Contracts.Enums.Progress.Rank)value.Rank,
        (PocketQuests.Contracts.Enums.Quests.Effort)value.Effort,
        value.Attributes.Select(item => item.ToModel()).ToImmutableArray(),
        value.Skills.Select(item => item.ToModel()).ToImmutableArray(),
        value.IsCustom,
        value.Description,
        value.PenaltyPercent,
        value.BaseXp);

    /// <summary>Maps QuestDefinition explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.QuestDefinition ToModel(this PocketQuests.Domain.Models.Quests.QuestDefinition value) => new(
        value.Quest.ToModel(),
        value.Revision,
        value.Archived);

    /// <summary>Maps Share explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.Share ToModel(this PocketQuests.Domain.Models.Quests.Share value) => new(
        value.Id,
        value.BasisPoints);

    /// <summary>Maps Completion explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.Completion ToModel(this PocketQuests.Domain.Models.Quests.Completion value) => new(
        value.Id,
        value.OccurrenceId,
        value.RecordedAt,
        value.Snapshot?.ToModel());

    /// <summary>Maps UndoEvent explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.UndoEvent ToModel(this PocketQuests.Domain.Models.Quests.UndoEvent value) => new(
        value.Id,
        value.CompletionId,
        value.RecordedAt);

    /// <summary>Maps Occurrence explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.Occurrence ToModel(this PocketQuests.Domain.Models.Quests.Occurrence value) => new(
        value.Id,
        value.Quest.ToModel(),
        value.DueDate,
        value.Deadline,
        value.AcceptedAt,
        value.PlannedTime,
        value.ParentId,
        value.Lifecycle?.ToModel());

    /// <summary>Maps OccurrenceLifecycle explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.OccurrenceLifecycle ToModel(this PocketQuests.Domain.Models.Quests.OccurrenceLifecycle value) => new(
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
