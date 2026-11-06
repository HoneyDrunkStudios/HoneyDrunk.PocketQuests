namespace PocketQuests.Contracts.Models.Profiles;

/// <summary>The public CustomSkill JSON contract, independent of storage and domain behavior.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Revision">The immutable resource revision.</param>
/// <param name="Archived">Whether the selected definition is archived.</param>
public sealed record CustomSkill(string Id, string Name, int Revision, bool Archived);
