namespace PocketQuests.Contracts.Models.Administration;

/// <summary>Minimal product profile and current moderation state, excluding private quest content and provider data.</summary>
public sealed record AdminUser(string UserId, string? CharacterName, string Zone, bool OnboardingComplete,
    DateTimeOffset CreatedAt, bool Suspended, string? Reason, int Revision);
