namespace PocketQuests.Contracts.Responses.Billing;

/// <summary>Account plan and permitted actions for the authenticated viewer.</summary>
/// <param name="Plan">Free, Plus or Lifetime Plus.</param>
/// <param name="ActiveCustomQuests">Number of unarchived custom quests.</param>
/// <param name="FreeQuestLimit">Maximum active custom quests for Free accounts.</param>
/// <param name="CheckoutAvailable">Whether hosted checkout can be started.</param>
/// <param name="PendingInterval">The reserved interval, when an open checkout can be resumed.</param>
/// <param name="PortalUrl">Stripe-hosted account management entry point, when configured for the subscriber.</param>
public sealed record BillingStatus(string Plan, int ActiveCustomQuests, int FreeQuestLimit, bool CheckoutAvailable, string? PendingInterval, string? PortalUrl);
