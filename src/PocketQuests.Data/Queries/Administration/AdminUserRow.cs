namespace PocketQuests.Data.Queries.Administration;

/// <summary>Selected product directory fields, not an Identity/provider directory.</summary>
public sealed record AdminUserRow(string UserId, string? CharacterName, string Zone, bool OnboardingComplete,
    DateTimeOffset CreatedAt, bool Suspended, string? Reason, int Revision);
