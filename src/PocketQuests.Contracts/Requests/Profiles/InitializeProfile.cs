namespace PocketQuests.Contracts.Requests.Profiles;

/// <summary>Initial account timezone selected from the device, validated against TZDB.</summary>
/// <param name="Zone">The applicable account time zone.</param>
public record InitializeProfile(string Zone);
