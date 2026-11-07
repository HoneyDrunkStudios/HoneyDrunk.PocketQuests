namespace PocketQuests.Contracts.Responses.Billing;

/// <summary>A hosted checkout URL returned to the account that started it.</summary>
/// <param name="Url">The hosted Stripe Checkout URL.</param>
public sealed record BillingCheckout(string Url);
