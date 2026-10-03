namespace PocketQuests.Domain.Profiles;

/// <summary>An account-owned skill with a stable ID; archival preserves earned history.</summary>
public record CustomSkill(string Id, string Name, int Revision = 1, bool Archived = false);
