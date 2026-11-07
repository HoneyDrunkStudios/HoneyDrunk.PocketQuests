using HoneyDrunk.Payments.Abstractions;
using HoneyDrunk.Payments.Stripe;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using PocketQuests.Services.Billing;

namespace PocketQuests.Api.Billing;

internal static class StripeBillingRegistration
{
    internal static void AddStripeBilling(this WebApplicationBuilder builder)
    {
        var billing = builder.Configuration.GetSection("Billing");
        var disableAutomaticTaxInSandbox = billing.GetValue<bool>("SandboxEnabled")
            && billing.GetValue<bool>("SandboxDisableAutomaticTax");
        builder.Services.Configure<BillingOptions>(billing);
        builder.Services.AddScoped(services => new StripeSecrets(builder.Configuration, builder.Environment, services));
        builder.Services.AddScoped<IStripeApiKeyProvider>(services => services.GetRequiredService<StripeSecrets>());
        builder.Services.AddScoped<IStripeWebhookSecretProvider>(services => services.GetRequiredService<StripeSecrets>());
        builder.Services.AddScoped(services => new StripeBillingClient(
            services.GetRequiredService<IStripeApiKeyProvider>(),
            disableAutomaticTaxInSandbox: disableAutomaticTaxInSandbox));
        builder.Services.AddScoped<IPaymentSubscriptionLifecycleClient>(services => services.GetRequiredService<StripeBillingClient>());
        builder.Services.AddScoped<IPaymentInvoiceReconciliationClient>(services => services.GetRequiredService<StripeBillingClient>());
        builder.Services.AddScoped<IPaymentWebhookEventValidator, StripeWebhookEventValidator>();
    }

    private sealed class StripeSecrets(IConfiguration configuration, IWebHostEnvironment environment, IServiceProvider services)
        : IStripeApiKeyProvider, IStripeWebhookSecretProvider
    {
        public async ValueTask<string> GetApiKeyAsync(CancellationToken cancellationToken)
        {
            var key = await Resolve("StripeApiKey", cancellationToken);
            return key.StartsWith("rk_test_", StringComparison.Ordinal) ? key : throw new BillingUnavailableException();
        }

        public async ValueTask<string> GetWebhookSecretAsync(CancellationToken cancellationToken)
        {
            var secret = await Resolve("StripeWebhookSecret", cancellationToken);
            return secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret : throw new BillingUnavailableException();
        }

        private async ValueTask<string> Resolve(string setting, CancellationToken token)
        {
            try
            {
                if (configuration[$"Billing:{setting}Name"] is { Length: > 0 } name)
                    return (await services.GetRequiredService<ISecretStore>().GetSecretAsync(new SecretIdentifier(name), token)).Value;
                if ((environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                    && configuration[$"Billing:{setting}"] is { Length: > 0 } secret)
                    return secret;
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                throw new BillingUnavailableException();
            }

            throw new BillingUnavailableException();
        }
    }
}
