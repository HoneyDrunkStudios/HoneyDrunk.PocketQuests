namespace PocketQuests.Domain.Models.Quests;

/// <summary>A stable recipient identifier and its share in integer basis points.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="BasisPoints">The rate expressed in basis points.</param>
public record Share(string Id, int BasisPoints)
{
    /// <summary>A complete reward pool expressed in basis points.</summary>
    public const int FullPoolBasisPoints = 10_000;
}
