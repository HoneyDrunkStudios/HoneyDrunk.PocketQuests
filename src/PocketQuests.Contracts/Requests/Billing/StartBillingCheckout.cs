namespace PocketQuests.Contracts.Requests.Billing;

/// <summary>Selects one of the server-configured Plus prices.</summary>
/// <param name="Interval">Monthly or yearly.</param>
public sealed record StartBillingCheckout(string Interval);
