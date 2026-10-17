namespace PocketQuests.Contracts.Requests.Profiles;

/// <summary>Initial account timezone selected from the device, validated against TZDB.</summary>
public record InitializeProfile(string Zone);
