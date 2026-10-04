namespace PocketQuests.Api.Contracts.Progress;

/// <summary>Explicit mappings for the progress HTTP contracts.</summary>
public static class ProgressContractMapping
{
    /// <summary>Maps Balance explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.Balance ToContract(this PocketQuests.Domain.Models.Progress.Balance value) => new(
        value.Id,
        value.Name,
        value.Xp,
        value.Level);

    /// <summary>Maps Entitlement explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.Entitlement ToContract(this PocketQuests.Domain.Models.Progress.Entitlement value) => new(
        value.Id,
        value.Kind,
        value.Name,
        value.Count,
        value.RequiredCount,
        (PocketQuests.Api.Contracts.Progress.Rank)value.RequiredRank,
        value.Earned);

    /// <summary>Maps RankProgress explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.RankProgress ToContract(this PocketQuests.Domain.Models.Progress.RankProgress value) => new(
        (PocketQuests.Api.Contracts.Progress.Rank)value.Current,
        value.Requirement.ToContract(),
        value.QualifyingCategories,
        value.Total);

    /// <summary>Maps RankRule explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.RankRule ToContract(this PocketQuests.Domain.Models.Progress.RankRule value) => new(
        (PocketQuests.Api.Contracts.Progress.Rank)value.Rank,
        value.Count,
        value.Floor,
        value.Total);

    /// <summary>Maps Streak explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.Streak ToContract(this PocketQuests.Domain.Models.Progress.Streak value) => new(
        value.CategoryId,
        value.Days,
        value.QualifiedToday,
        value.Rate);

    /// <summary>Maps XpEntry explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Progress.XpEntry ToContract(this PocketQuests.Domain.Models.Progress.XpEntry value) => new(
        value.EventId,
        value.OccurrenceId,
        value.At,
        value.Track,
        value.TrackId,
        value.Amount);
}
