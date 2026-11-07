using HoneyDrunk.Payments.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Contracts.Requests.Billing;
using PocketQuests.Contracts.Responses.Billing;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <content>Account plan enforcement and its authenticated status endpoint.</content>
public sealed partial class SqlApiTests
{
    /// <summary>Verifies the five-quest limit across concurrent requests, archiving, replay and a lifetime grant.</summary>
    /// <returns>Completion of the real SQL checks.</returns>
    [Fact]
    public async Task BillingCapAndLifetimeGrantAreAccountScoped()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("billing-alice");
        using var other = host.Client("billing-other");
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/billing", UriKind.Relative))).StatusCode);
        await Setup(alice);
        var first = BillingCustomQuest();
        await Command(alice, first);
        for (var index = 0; index < 3; index++)
            await Command(alice, BillingCustomQuest());
        await Command(alice, new(Guid.NewGuid(), QuestActions.Accept, QuestId: "PQ-CAT-Q01"));
        var candidates = new[] { BillingCustomQuest(), BillingCustomQuest() };
        var responses = await Task.WhenAll(candidates.Select(candidate => alice.PostAsJsonAsync("/api/commands", candidate)));
        var accepted = Array.FindIndex(responses, response => response.IsSuccessStatusCode);
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        var free = await alice.GetFromJsonAsync<BillingStatus>("/api/billing");
        Assert.Equal("Free", free!.Plan);
        Assert.Equal(5, free.ActiveCustomQuests);
        Assert.False(free.CheckoutAvailable);
        Assert.Equal(5, (await Command(alice, candidates[accepted])).Definitions.Count(definition => !definition.Archived && definition.Quest.IsCustom));
        var edited = await Command(alice, first with { OperationId = Guid.NewGuid(), ExpectedRevision = 1 });
        var revision = edited.Definitions.Single(definition => definition.Quest.Id == first.Definition!.Id).Revision;
        await Command(alice, new(Guid.NewGuid(), QuestActions.ArchiveDefinition, QuestId: first.Definition!.Id, ExpectedRevision: revision));
        await Command(alice, candidates[1 - accepted]);

        await using (var db = Context())
        {
            var account = await db.Read.Set<AccountEntity>().AsTracking().SingleAsync();
            var invalidCustomer = await Assert.ThrowsAsync<SqlException>(() => db.Read.Database.ExecuteSqlRawAsync(
                "UPDATE [pocketquests].[Account] SET [StripeCustomerId] = {0} WHERE [Id] = {1}",
                "cus_bad_id",
                account.Id));
            Assert.Equal(547, invalidCustomer.Number);
            Assert.Contains("CK_Account_StripeCustomerId", invalidCustomer.Message, StringComparison.Ordinal);
            account.FounderPlusGrantedAt = DateTimeOffset.UtcNow;
            Assert.Equal(1, await db.Read.SaveChangesAsync());
        }

        await Setup(other);
        Assert.Equal("Free", (await other.GetFromJsonAsync<BillingStatus>("/api/billing"))!.Plan);
        using var restarted = new Host(Connection);
        using var founder = restarted.Client("billing-alice");
        await Command(founder, BillingCustomQuest());
        var lifetime = await founder.GetFromJsonAsync<BillingStatus>("/api/billing");
        Assert.Equal("Lifetime Plus", lifetime!.Plan);
        Assert.Equal(6, lifetime.ActiveCustomQuests);
        Assert.False(lifetime.CheckoutAvailable);
    }

    /// <summary>Exercises reservation and entitlement with a deterministic HoneyDrunk.Payments provider.</summary>
    /// <returns>Completion of the checkout and renewal regression.</returns>
    [Fact]
    public async Task BillingCheckoutLinksVerifiedSandboxSubscriptionAndRequiresPayment()
    {
        var payments = new SandboxPayments { FailNextCheckout = true };
        var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(-10));
        using var product = new Host(Connection, clock: clock);
        using var original = product.Client("billing-subscriber");
        using var host = product.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:SandboxEnabled"] = "true",
                ["Billing:MonthlyPriceId"] = "price_testmonthly",
                ["Billing:YearlyPriceId"] = "price_testyearly",
                ["Billing:ReturnUrl"] = "https://app.example.test/settings",
            }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IPaymentSubscriptionLifecycleClient>(payments);
                services.AddSingleton<IPaymentInvoiceReconciliationClient>(payments);
                services.AddSingleton<IPaymentWebhookEventValidator>(payments);
            });
        });
        using var subscriber = host.CreateClient();
        subscriber.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        await Setup(subscriber);
        for (var index = 0; index < 5; index++)
            await Command(subscriber, BillingCustomQuest());
        var sixth = BillingCustomQuest();
        Assert.True((await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing"))!.CheckoutAvailable);
        using var first = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("yearly"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        clock.Now = clock.Now.AddMinutes(10);
        using var retry = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("yearly"));
        retry.EnsureSuccessStatusCode();
        using var resume = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("yearly"));
        resume.EnsureSuccessStatusCode();
        using var otherInterval = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("monthly"));
        Assert.Equal(HttpStatusCode.BadRequest, otherInterval.StatusCode);
        Assert.Equal(3, payments.Checkouts.Count);
        Assert.All(payments.Checkouts, checkout => Assert.Equal(payments.Checkouts[0].IdempotencyKey, checkout.IdempotencyKey));
        Assert.Equal("price_testyearly", payments.Checkouts[0].ProviderPriceId);
        Assert.EndsWith("@example.com", payments.Checkouts[0].CustomerEmail, StringComparison.Ordinal);
        await Webhook("forged", HttpStatusCode.BadRequest);
        await Webhook("live", HttpStatusCode.BadRequest);
        Assert.Equal("Free", (await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing"))!.Plan);
        await Webhook("sandbox", HttpStatusCode.NoContent);
        await Webhook("sandbox", HttpStatusCode.NoContent);
        using var unpaid = await subscriber.PostAsJsonAsync("/api/commands", sixth);
        Assert.Equal(HttpStatusCode.BadRequest, unpaid.StatusCode);
        payments.Paid = true;
        Assert.Equal("Plus", (await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing"))!.Plan);
        await Command(subscriber, sixth);
        payments.Status = "canceled";
        var downgraded = await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing");
        Assert.Equal("Free", downgraded!.Plan);
        Assert.Equal(6, downgraded.ActiveCustomQuests);
        using var blocked = await subscriber.PostAsJsonAsync("/api/commands", BillingCustomQuest());
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        clock.Now = clock.Now.AddMinutes(1);
        using var monthly = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("monthly"));
        monthly.EnsureSuccessStatusCode();
        var reserved = payments.Checkouts[^1];
        Assert.Equal("price_testmonthly", reserved.ProviderPriceId);
        Assert.Equal("cus_PocketQuests", reserved.ProviderCustomerId);
        Assert.Null(reserved.CustomerEmail);

        var expiryMetadata = new Dictionary<string, string>
        {
            ["project_id"] = reserved.ProjectId,
            ["payments_tenant_id"] = reserved.TenantId,
            ["tier_name"] = reserved.TierName,
        };
        var staleExpiration = new PaymentWebhookEventSnapshot(
            "stripe",
            "evt_StaleExpiry",
            "checkout.session.expired",
            clock.Now.UtcDateTime,
            false,
            "cs_test_Unrelated",
            "checkout.session",
            expiryMetadata);
        payments.Expiration = staleExpiration;
        await Webhook("expiration", HttpStatusCode.NoContent);
        Assert.Equal("monthly", (await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing"))!.PendingInterval);

        payments.Expiration = staleExpiration with { ProviderEventId = "evt_CurrentExpiry", ObjectId = "cs_test_PocketQuests" };
        await Webhook("expiration", HttpStatusCode.NoContent);
        Assert.Null((await subscriber.GetFromJsonAsync<BillingStatus>("/api/billing"))!.PendingInterval);
        clock.Now = clock.Now.AddMinutes(1);
        using var replacement = await subscriber.PostAsJsonAsync("/api/billing/checkout", new StartBillingCheckout("yearly"));
        replacement.EnsureSuccessStatusCode();
        Assert.NotEqual(reserved.IdempotencyKey, payments.Checkouts[^1].IdempotencyKey);

        async Task Webhook(string signature, HttpStatusCode expected)
        {
            using var webhook = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/billing/stripe/webhook", UriKind.Relative))
            {
                Content = new StringContent("{}"),
            };
            webhook.Headers.Add("Stripe-Signature", signature);
            using var anonymous = host.CreateClient();
            using var response = await anonymous.SendAsync(webhook);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    private static QuestCommand BillingCustomQuest() => new(
        Guid.NewGuid(),
        QuestActions.SaveDefinition,
        Definition: new Quest(Guid.NewGuid().ToString("D"), "Custom quest", "Complete the task", "c03", Rank.F, Effort.Small, [], [], true),
        ExpectedRevision: 0);

    private sealed class SandboxPayments : IPaymentSubscriptionLifecycleClient, IPaymentInvoiceReconciliationClient, IPaymentWebhookEventValidator
    {
        private static readonly IReadOnlyDictionary<string, string> Metadata = new Dictionary<string, string>();
        private static readonly DateTime Created = DateTime.UtcNow;

        internal List<PaymentCheckoutSessionRequest> Checkouts { get; } = [];
        internal bool FailNextCheckout { get; set; }
        internal bool Paid { get; set; }
        internal string Status { get; set; } = "active";
        internal PaymentWebhookEventSnapshot? Expiration { get; set; }

        public ValueTask<PaymentCheckoutSessionSnapshot> CreateCheckoutSessionAsync(PaymentCheckoutSessionRequest request, CancellationToken cancellationToken)
        {
            Checkouts.Add(request);
            if (FailNextCheckout)
            {
                FailNextCheckout = false;
                throw new HttpRequestException("Checkout response unavailable.");
            }

            return ValueTask.FromResult(new PaymentCheckoutSessionSnapshot("stripe", "cs_test_PocketQuests", "https://checkout.stripe.com/c/pay/test", request.TenantId, request.ProjectId, request.TierName, null, null));
        }

        public ValueTask<PaymentSubscriptionSnapshot> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken)
        {
            var checkout = Checkouts[0];
            return ValueTask.FromResult(new PaymentSubscriptionSnapshot("stripe", providerSubscriptionId, "cus_PocketQuests", Status, checkout.TenantId, checkout.ProjectId, checkout.TierName, false, null, DateTime.UtcNow, "in_PocketQuests", Metadata));
        }

        public ValueTask<PaymentSubscriptionSnapshot> CancelSubscriptionAsync(PaymentSubscriptionCancellationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<PaymentInvoiceReconciliationSnapshot> ReconcileInvoiceAsync(string providerInvoiceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new PaymentInvoiceReconciliationSnapshot("stripe", providerInvoiceId, "cus_PocketQuests", "sub_PocketQuests", Paid ? "paid" : "open", "usd", 3999, Paid ? 3999 : 0, Paid ? 0 : 3999, Created, Created.AddYears(1), Paid ? Created : null, null, null, null, null, null, Metadata));

        public ValueTask<PaymentWebhookEventSnapshot> ValidateWebhookEventAsync(string payload, string signatureHeader, CancellationToken cancellationToken) =>
            signatureHeader switch
            {
                "sandbox" or "live" => ValueTask.FromResult(new PaymentWebhookEventSnapshot("stripe", "evt_PocketQuests", "invoice.paid", Created, signatureHeader == "live", "in_PocketQuests", "invoice", Metadata)),
                "expiration" when Expiration is { } expiration => ValueTask.FromResult(expiration),
                _ => throw new InvalidOperationException("Invalid test signature."),
            };
    }
}
