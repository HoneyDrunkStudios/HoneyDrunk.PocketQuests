using HoneyDrunk.Payments.Abstractions;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Services.Billing;

internal sealed class PlusAccess(IPaymentSubscriptionLifecycleClient? subscriptions, IPaymentInvoiceReconciliationClient? invoices)
{
    internal const int FreeQuestLimit = 5;
    internal const string Project = "pocket-quests";

    internal static bool Owns(Guid accountId, PaymentSubscriptionSnapshot subscription) =>
        subscription.Provider == PaymentProviderNames.Stripe && subscription.ProjectId == Project
        && subscription.TierName is "plus-monthly" or "plus-yearly"
        && subscription.TenantId == accountId.ToString("D");

    internal static async Task<T> ProviderCall<T>(Func<ValueTask<T>> call, CancellationToken token)
    {
        try
        {
            return await call();
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // Provider errors can contain request data. The public boundary exposes a safe, retryable message.
            throw new BillingUnavailableException();
        }
    }

    internal async Task<bool> HasPlus(AccountEntity account, CancellationToken token) =>
        account.FounderPlusGrantedAt is not null || await IsPaid(account, await ReadLinked(account, token), token);

    internal async Task<PaymentSubscriptionSnapshot?> ReadLinked(AccountEntity account, CancellationToken token)
    {
        if (account.StripeSubscriptionId is not { } id)
            return null;

        var subscription = await ReadSubscription(id, token);
        if (!Owns(account.Id, subscription) || subscription.ProviderCustomerId != account.StripeCustomerId)
            throw new BillingUnavailableException();
        return subscription;
    }

    internal Task<PaymentSubscriptionSnapshot> ReadSubscription(string id, CancellationToken token) =>
        ProviderCall(() => (subscriptions ?? throw new BillingUnavailableException()).GetSubscriptionAsync(id, token), token);

    internal Task<PaymentInvoiceReconciliationSnapshot> ReadInvoice(string id, CancellationToken token) =>
        ProviderCall(() => (invoices ?? throw new BillingUnavailableException()).ReconcileInvoiceAsync(id, token), token);

    internal async Task<bool> IsPaid(AccountEntity account, PaymentSubscriptionSnapshot? subscription, CancellationToken token)
    {
        if (subscription is not { Status: "active", LatestInvoiceId: { } invoiceId })
            return false;
        var invoice = await ReadInvoice(invoiceId, token);
        return invoice.Status == "paid" && invoice.AmountRemaining == 0 && invoice.AmountPaid > 0
            && invoice.ProviderSubscriptionId == subscription.ProviderSubscriptionId
            && invoice.ProviderCustomerId == account.StripeCustomerId;
    }
}
