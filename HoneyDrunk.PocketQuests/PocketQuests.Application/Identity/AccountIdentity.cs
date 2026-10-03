namespace PocketQuests.Application.Identity;

// Identity comes from validated issuer+subject, never an account ID in a request.
/// <summary>A trusted identity resolved by HoneyDrunk.Identity, never supplied as a request account ID.</summary>
public record AccountIdentity(string Issuer, string Subject);
