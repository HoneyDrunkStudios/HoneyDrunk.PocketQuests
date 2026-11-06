namespace PocketQuests.Contracts.Models.Administration;

/// <summary>Minimal product profile and current moderation state, excluding private quest content and provider data.</summary>
/// <param name="UserId">The canonical account user identifier.</param>
/// <param name="CharacterName">The account character display name.</param>
/// <param name="Zone">The applicable account time zone.</param>
/// <param name="OnboardingComplete">Whether the account has completed onboarding.</param>
/// <param name="CreatedAt">The resource creation timestamp.</param>
/// <param name="Suspended">Whether account access is suspended.</param>
/// <param name="Reason">The human-readable reason associated with this result.</param>
/// <param name="Revision">The immutable resource revision.</param>
public sealed record AdminUser(
    string UserId,
    string? CharacterName,
    string Zone,
    bool OnboardingComplete,
    DateTimeOffset CreatedAt,
    bool Suspended,
    string? Reason,
    int Revision);
