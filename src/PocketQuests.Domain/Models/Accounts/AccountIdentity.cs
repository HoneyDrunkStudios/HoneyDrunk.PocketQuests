namespace PocketQuests.Domain.Models.Accounts;

// Identity comes from validated issuer+subject, never an account ID in a request.

/// <summary>A trusted identity resolved by HoneyDrunk.Identity, never supplied as a request account ID.</summary>
/// <param name="Issuer">The issuer of the recorded-clock evidence.</param>
/// <param name="Subject">The authenticated account subject.</param>
public record AccountIdentity(string Issuer, string Subject);
