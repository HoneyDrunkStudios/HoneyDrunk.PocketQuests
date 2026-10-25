namespace PocketQuests.DevelopmentHost;

/// <summary>Private pairing proof for the opt-in local development host.</summary>
/// <param name="PairingCode">Code entered on the device; never embedded in its bundle.</param>
public sealed record PairingRequest(string PairingCode);
