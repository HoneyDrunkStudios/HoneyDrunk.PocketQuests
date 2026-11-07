using HoneyDrunk.Payments.Abstractions;
using HoneyDrunk.Payments.Stripe;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Services.Billing;

namespace PocketQuests.Tests.Api;

/// <summary>Checks the real API's sandbox billing registration without calling Stripe or Vault.</summary>
public sealed class ApiBillingTests
{
    /// <summary>Only valid sandbox credentials can reach the shared payment clients.</summary>
    /// <param name="apiKey">The synthetic local Stripe key.</param>
    /// <param name="webhookSecret">The synthetic local signing secret.</param>
    /// <param name="useVault">Whether a configured Vault lookup takes precedence over local credentials.</param>
    /// <param name="expectedToResolve">Whether the configured credentials are accepted.</param>
    /// <returns>Completion of the service composition assertions.</returns>
    [Theory]
    [InlineData("rk_test_fixture", "whsec_fixture", false, true)]
    [InlineData("rk_live_fixture", "invalid_fixture", false, false)]
    [InlineData(null, null, false, false)]
    [InlineData("rk_test_fixture", "whsec_fixture", true, false)]
    public async Task BillingRegistrationEnforcesSandboxCredentials(string? apiKey, string? webhookSecret, bool useVault, bool expectedToResolve)
    {
        using var product = new ApiApplicationFactory();
        using var host = product.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:SandboxEnabled"] = "true",
                ["Billing:SandboxDisableAutomaticTax"] = "true",
                ["Billing:StripeApiKey"] = apiKey,
                ["Billing:StripeWebhookSecret"] = webhookSecret,
                ["Billing:StripeApiKeyName"] = useVault ? "stripe-key-fixture" : null,
                ["Billing:StripeWebhookSecretName"] = useVault ? "stripe-hook-fixture" : null,
            }));
            builder.ConfigureServices(services => services.AddScoped<ISecretStore>(_ => throw new InvalidOperationException("Vault fixture is offline.")));
        });
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var key = services.GetRequiredService<IStripeApiKeyProvider>();
        var signingSecret = services.GetRequiredService<IStripeWebhookSecretProvider>();

        if (expectedToResolve)
        {
            Assert.Equal("rk_test_fixture", await key.GetApiKeyAsync(default));
            Assert.Equal("whsec_fixture", await signingSecret.GetWebhookSecretAsync(default));
            var client = Assert.IsType<StripeBillingClient>(services.GetRequiredService<IPaymentSubscriptionLifecycleClient>());
            Assert.Same(client, services.GetRequiredService<IPaymentInvoiceReconciliationClient>());
            Assert.IsType<StripeWebhookEventValidator>(services.GetRequiredService<IPaymentWebhookEventValidator>());
            return;
        }

        await Assert.ThrowsAsync<BillingUnavailableException>(() => key.GetApiKeyAsync(default).AsTask());
        await Assert.ThrowsAsync<BillingUnavailableException>(() => signingSecret.GetWebhookSecretAsync(default).AsTask());
    }
}
