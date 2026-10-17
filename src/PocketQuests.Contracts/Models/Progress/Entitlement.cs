using PocketQuests.Contracts.Enums.Progress;

namespace PocketQuests.Contracts.Models.Progress;

/// <summary>The public Entitlement JSON contract, independent of storage and domain behavior.</summary>
public sealed record Entitlement(string Id, string Kind, string Name, int Count, int RequiredCount, Rank RequiredRank, bool Earned);
