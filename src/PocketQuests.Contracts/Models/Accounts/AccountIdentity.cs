namespace PocketQuests.Contracts.Models.Accounts;

/// <summary>Canonical account identity established by the trusted host authentication boundary.</summary>
/// <param name="Issuer">The issuer of the recorded-clock evidence.</param>
/// <param name="Subject">The authenticated account subject.</param>
public sealed record AccountIdentity(string Issuer, string Subject);
