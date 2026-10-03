namespace PocketQuests.Domain.Quests.Definitions;

/// <summary>A stable recipient identifier and its share in integer basis points.</summary>
public record Share(string Id, int BasisPoints)
{
    /// <summary>A complete reward pool expressed in basis points.</summary>
    public const int FullPoolBasisPoints = 10_000;
}
