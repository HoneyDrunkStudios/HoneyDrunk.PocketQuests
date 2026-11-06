namespace PocketQuests.Domain.Models.Skills;

/// <summary>An account-owned skill with a stable ID; archival preserves earned history.</summary>
/// <param name="Id">The identifier of the represented resource.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="Revision">The immutable resource revision.</param>
/// <param name="Archived">Whether the selected definition is archived.</param>
public record CustomSkill(string Id, string Name, int Revision = 1, bool Archived = false);
