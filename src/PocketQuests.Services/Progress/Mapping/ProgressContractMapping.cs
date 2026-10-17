namespace PocketQuests.Services.Progress.Mapping;

/// <summary>Explicit mappings for the progress HTTP contracts.</summary>
public static class ProgressContractMapping
{
    /// <summary>Maps Balance explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.Balance ToModel(this PocketQuests.Domain.Models.Progress.Balance value) => new(
        value.Id,
        value.Name,
        value.Xp,
        value.Level);

    /// <summary>Maps Entitlement explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.Entitlement ToModel(this PocketQuests.Domain.Models.Progress.Entitlement value) => new(
        value.Id,
        value.Kind,
        value.Name,
        value.Count,
        value.RequiredCount,
        (PocketQuests.Contracts.Enums.Progress.Rank)value.RequiredRank,
        value.Earned);

    /// <summary>Maps RankProgress explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.RankProgress ToModel(this PocketQuests.Domain.Models.Progress.RankProgress value) => new(
        (PocketQuests.Contracts.Enums.Progress.Rank)value.Current,
        value.Requirement.ToModel(),
        value.QualifyingCategories,
        value.Total);

    /// <summary>Maps RankRule explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.RankRule ToModel(this PocketQuests.Domain.Models.Progress.RankRule value) => new(
        (PocketQuests.Contracts.Enums.Progress.Rank)value.Rank,
        value.Count,
        value.Floor,
        value.Total);

    /// <summary>Maps Streak explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.Streak ToModel(this PocketQuests.Domain.Models.Progress.Streak value) => new(
        value.CategoryId,
        value.Days,
        value.QualifiedToday,
        value.Rate);

    /// <summary>Maps XpEntry explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Progress.XpEntry ToModel(this PocketQuests.Domain.Models.Progress.XpEntry value) => new(
        value.EventId,
        value.OccurrenceId,
        value.At,
        value.Track,
        value.TrackId,
        value.Amount);
}
