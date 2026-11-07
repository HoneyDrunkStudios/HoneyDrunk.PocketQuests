namespace PocketQuests.Services.Billing;

/// <summary>Indicates that the billing provider could not confirm the requested operation.</summary>
public sealed class BillingUnavailableException() : Exception("Billing is temporarily unavailable. Try again in a moment.");
