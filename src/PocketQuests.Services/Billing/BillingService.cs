using HoneyDrunk.Payments.Abstractions;
using Microsoft.Extensions.Options;
using PocketQuests.Contracts.Responses.Billing;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Errors;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Billing;

/// <summary>Owns Pocket Quests plan policy and links verified provider subscriptions to accounts.</summary>
public sealed class BillingService
{
    // Stay inside Stripe's minimum idempotency retention window. A signed expiry event
    // releases the reservation so a missing completion event cannot cause a second charge.
    private static readonly TimeSpan ResumeWindow = TimeSpan.FromHours(23);
    private readonly IAccountDataService accounts;
    private readonly ICurrentAccount viewer;
    private readonly QuestService quests;
    private readonly PlusAccess access;
    private readonly BillingOptions options;
    private readonly IPaymentSubscriptionLifecycleClient? subscriptions;
    private readonly TimeProvider clock;

    internal BillingService(
        IAccountDataService accounts,
        ICurrentAccount viewer,
        QuestService quests,
        PlusAccess access,
        IOptions<BillingOptions> options,
        IPaymentSubscriptionLifecycleClient? subscriptions,
        TimeProvider clock)
    {
        this.accounts = accounts;
        this.viewer = viewer;
        this.quests = quests;
        this.access = access;
        this.options = options.Value;
        this.subscriptions = subscriptions;
        this.clock = clock;
    }

    /// <summary>Reads current plan and usage without trusting any client-supplied account identifiers.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current billing status.</returns>
    public async Task<BillingStatus> Read(CancellationToken token)
    {
        var identity = viewer.Identity.ToModel();
        await quests.RequireAccess(identity, false, token);
        var account = await quests.Account(identity, token);
        var active = await accounts.CountActiveCustomQuests(account.Id, token);
        if (account.FounderPlusGrantedAt is not null)
            return new("Lifetime Plus", active, PlusAccess.FreeQuestLimit, false, null);

        var subscription = await access.ReadLinked(account, token);
        var paid = await access.IsPaid(account, subscription, token);
        return new(
            paid ? "Plus" : "Free",
            active,
            PlusAccess.FreeQuestLimit,
            options.Ready && subscriptions is not null && !HasOpenSubscription(subscription),
            account.PlusCheckoutInterval);
    }

    /// <summary>Reserves and starts a retry-safe hosted Checkout session for the current account.</summary>
    /// <param name="interval">Monthly or yearly.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The account's hosted session.</returns>
    public async Task<BillingCheckout> StartCheckout(string interval, CancellationToken token)
    {
        if (interval is not ("monthly" or "yearly"))
            throw new QuestValidationException("Choose monthly or yearly Plus.");
        if (!options.Ready || subscriptions is null)
            throw new BillingUnavailableException();

        var identity = viewer.Identity.ToModel();
        var now = clock.GetUtcNow();
        var reservation = await accounts.ExecuteInTransaction(Reserve, token);

        // Keep the reservation committed even if the provider responds and the request disconnects.
        var request = new PaymentCheckoutSessionRequest(
            reservation.id.ToString("D"),
            PlusAccess.Project,
            $"plus-{interval}",
            (interval == "monthly" ? options.MonthlyPriceId : options.YearlyPriceId)!,
            options.ReturnUrl!,
            options.ReturnUrl!,
            FormattableString.Invariant($"pq-{reservation.id:N}-{reservation.started.UtcTicks}"),
            reservation.customerId,
            CustomerEmail: reservation.customerId is null ? SandboxEmail(reservation.id) : null);
        var session = await PlusAccess.ProviderCall(() => subscriptions.CreateCheckoutSessionAsync(request, token), token);
        if (!Uri.TryCreate(session.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps
            || url.Host != "checkout.stripe.com" || url.UserInfo.Length != 0 || !ValidReference(session.SessionId, "cs_test_", 120))
            throw new BillingUnavailableException();

        await accounts.ExecuteInTransaction(RecordSession, token);
        return new(url.AbsoluteUri);

        async Task<(Guid id, string? customerId, DateTimeOffset started)> Reserve(CancellationToken tx)
        {
            await quests.RequireAccess(identity, true, tx);
            var account = await quests.Account(identity, tx);
            if (account.FounderPlusGrantedAt is not null || HasOpenSubscription(await access.ReadLinked(account, tx)))
                throw new QuestValidationException("This account already has Plus or a subscription in progress.");
            if (account.PlusCheckoutStartedAt is { } started)
            {
                var age = now - started;
                if (account.PlusCheckoutInterval != interval || age >= ResumeWindow)
                    throw new QuestValidationException("This account has a pending checkout. Finish that checkout or try again after it expires.");
            }
            else
            {
                account.StripeSubscriptionId = null;
                account.PlusCheckoutStartedAt = now;
                account.PlusCheckoutInterval = interval;
                account.StripeCheckoutSessionId = null;
                Touch(account, now);
            }

            return (id: account.Id, customerId: account.StripeCustomerId, started: account.PlusCheckoutStartedAt!.Value);
        }

        async Task<bool> RecordSession(CancellationToken tx)
        {
            await quests.RequireAccess(identity, true, tx);
            var account = await quests.Account(identity, tx);
            if (account.PlusCheckoutStartedAt == reservation.started && account.PlusCheckoutInterval == interval)
            {
                account.StripeCheckoutSessionId = session.SessionId;
                Touch(account, clock.GetUtcNow());
            }

            return true;
        }
    }

    /// <summary>Links the provider's current subscription after a verified sandbox event.</summary>
    /// <param name="message">A signed event normalized by HoneyDrunk.Payments.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the verified update.</returns>
    public async Task ApplyEvent(PaymentWebhookEventSnapshot message, CancellationToken token)
    {
        if (!options.Ready || message.Provider != PaymentProviderNames.Stripe || message.Livemode || message.ObjectId is null)
            return;

        if (message.EventType == "checkout.session.expired")
        {
            await ExpireReservation(message, token);
            return;
        }

        var subscriptionId = message.EventType switch
        {
            "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted" => message.ObjectId,
            "invoice.paid" or "invoice.payment_failed" => (await access.ReadInvoice(message.ObjectId, token)).ProviderSubscriptionId,
            _ => null,
        };
        if (!ValidReference(subscriptionId, "sub_", 80))
            return;

        var subscription = await access.ReadSubscription(subscriptionId!, token);
        if (!Guid.TryParseExact(subscription.TenantId, "D", out var accountId) || !PlusAccess.Owns(accountId, subscription)
            || !ValidReference(subscription.ProviderCustomerId, "cus_", 80) || subscription.ProviderSubscriptionId != subscriptionId)
            return;

        var owner = await accounts.GetIdentityByAccountId(accountId, token);
        if (owner is null)
            return;

        await accounts.ExecuteInTransaction(LinkSubscription, token);

        async Task<bool> LinkSubscription(CancellationToken tx)
        {
            await accounts.AcquireCommandLock(owner, tx);
            var account = await accounts.GetByIdentityUserId(owner, tx);
            if (account is null || account.Id != accountId || account.FounderPlusGrantedAt is not null
                || (account.StripeCustomerId is not null && account.StripeCustomerId != subscription.ProviderCustomerId)
                || (account.StripeSubscriptionId is not null && account.StripeSubscriptionId != subscriptionId))
                return false;

            if (account.StripeSubscriptionId is null)
            {
                // A delayed event from a prior checkout cannot attach to a new reservation.
                if (account.PlusCheckoutStartedAt is not { } started || subscription.CreatedAt < started.UtcDateTime.AddSeconds(-1)
                    || subscription.TierName != $"plus-{account.PlusCheckoutInterval}")
                    return false;
                account.StripeCustomerId = subscription.ProviderCustomerId;
                account.StripeSubscriptionId = subscriptionId;
                account.PlusCheckoutStartedAt = null;
                account.PlusCheckoutInterval = null;
                account.StripeCheckoutSessionId = null;
                Touch(account, clock.GetUtcNow());
            }

            return true;
        }
    }

    private static bool HasOpenSubscription(PaymentSubscriptionSnapshot? subscription) =>
        subscription is not null && subscription.Status is not ("canceled" or "incomplete_expired");

    // The shared adapter requires a customer or an email. Sandbox Checkout must
    // never send the sign-in email or any other real customer information.
    private static string SandboxEmail(Guid accountId) => $"pocketquests-plus+{accountId:N}@example.com";

    private static void Touch(AccountEntity account, DateTimeOffset now) =>
        account.ModifiedAt = now > account.ModifiedAt ? now : account.ModifiedAt;

    private static bool ValidReference(string? value, string prefix, int max) =>
        value is not null && value.Length > prefix.Length && value.Length <= max
        && value.StartsWith(prefix, StringComparison.Ordinal)
        && value[prefix.Length..].All(char.IsAsciiLetterOrDigit);

    private async Task ExpireReservation(PaymentWebhookEventSnapshot message, CancellationToken token)
    {
        if (!ValidReference(message.ObjectId, "cs_test_", 120)
            || message.Metadata.GetValueOrDefault("project_id") != PlusAccess.Project
            || !Guid.TryParseExact(message.Metadata.GetValueOrDefault("payments_tenant_id"), "D", out var accountId))
            return;
        var owner = await accounts.GetIdentityByAccountId(accountId, token);
        if (owner is null)
            return;

        await accounts.ExecuteInTransaction(Clear, token);

        async Task<bool> Clear(CancellationToken tx)
        {
            await accounts.AcquireCommandLock(owner, tx);
            var account = await accounts.GetByIdentityUserId(owner, tx);
            if (account is null || account.Id != accountId || account.PlusCheckoutStartedAt is not { } started
                || message.Metadata.GetValueOrDefault("tier_name") != $"plus-{account.PlusCheckoutInterval}")
                return false;
            var mismatched = account.StripeCheckoutSessionId is { } sessionId
                ? sessionId != message.ObjectId
                : message.CreatedAt < started.UtcDateTime.AddHours(23);
            if (mismatched)
                return false;

            account.PlusCheckoutStartedAt = null;
            account.PlusCheckoutInterval = null;
            account.StripeCheckoutSessionId = null;
            Touch(account, clock.GetUtcNow());
            return true;
        }
    }
}
