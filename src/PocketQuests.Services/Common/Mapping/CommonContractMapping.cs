namespace PocketQuests.Services.Common.Mapping;

/// <summary>Explicit mappings for the common HTTP contracts.</summary>
public static class CommonContractMapping
{
    /// <summary>Maps PenaltyAssessment explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Models.Quests.PenaltyAssessment ToModel(this PocketQuests.Domain.Models.Quests.PenaltyAssessment value) => new(
        value.OccurrenceId,
        value.CategoryId,
        value.LockedLoss,
        value.ActualLoss,
        value.At);
}
