namespace PocketQuests.Services.Billing;

/// <summary>Pocket Quests checkout catalog and return destinations.</summary>
public sealed class BillingOptions
{
    /// <summary>Gets or sets a value indicating whether the Stripe sandbox is available.</summary>
    public bool SandboxEnabled { get; set; }

    /// <summary>Gets or sets the server-configured monthly recurring price ID.</summary>
    public string? MonthlyPriceId { get; set; }

    /// <summary>Gets or sets the server-configured yearly recurring price ID.</summary>
    public string? YearlyPriceId { get; set; }

    /// <summary>Gets or sets the trusted web settings URL used on return from Checkout.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>Gets or sets the Stripe-hosted customer portal login URL.</summary>
    public string? PortalUrl { get; set; }

    internal bool Ready => SandboxEnabled
        && MonthlyPriceId?.StartsWith("price_", StringComparison.Ordinal) == true
        && YearlyPriceId?.StartsWith("price_", StringComparison.Ordinal) == true
        && MonthlyPriceId != YearlyPriceId
        && Uri.TryCreate(ReturnUrl, UriKind.Absolute, out var url)
        && url.UserInfo.Length == 0 && url.Fragment.Length == 0
        && (url.Scheme == Uri.UriSchemeHttps || (url.IsLoopback && url.Scheme == Uri.UriSchemeHttp));

    internal string? SafePortal => IsSafePortal() ? PortalUrl : null;

    private bool IsSafePortal() => Uri.TryCreate(PortalUrl, UriKind.Absolute, out var url)
        && url.Scheme == Uri.UriSchemeHttps && url.Host == "billing.stripe.com" && url.UserInfo.Length == 0;
}
