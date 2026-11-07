using HoneyDrunk.Payments.Abstractions;
using PocketQuests.Contracts.Responses.Billing;
using System.Globalization;

namespace PocketQuests.Services.Billing.Mapping;

internal static class BillingContractMapping
{
    internal static BillingStatus Status(string plan, int active, int limit, bool checkoutAvailable, string? pendingInterval) =>
        new(plan, active, limit, checkoutAvailable, pendingInterval);

    internal static BillingCheckout Checkout(Uri url) => new(url.AbsoluteUri);

    internal static PaymentCheckoutSessionRequest CheckoutRequest(
        Guid accountId,
        string? customerId,
        string interval,
        string priceId,
        string returnUrl,
        DateTimeOffset started,
        string? customerEmail) => new(
            accountId.ToString("D"),
            PlusAccess.Project,
            $"plus-{interval}",
            priceId,
            returnUrl,
            returnUrl,
            string.Create(CultureInfo.InvariantCulture, $"pq-{accountId:N}-{started.UtcTicks}"),
            customerId,
            CustomerEmail: customerEmail);
}
