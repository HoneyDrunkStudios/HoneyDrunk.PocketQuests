namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public CustomSkill JSON contract, independent of storage and domain behavior.</summary>
public sealed record CustomSkill(string Id, string Name, int Revision, bool Archived);
