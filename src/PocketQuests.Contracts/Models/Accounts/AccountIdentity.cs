namespace PocketQuests.Contracts.Models.Accounts;

/// <summary>Canonical account identity established by the trusted host authentication boundary.</summary>
public sealed record AccountIdentity(string Issuer, string Subject);
