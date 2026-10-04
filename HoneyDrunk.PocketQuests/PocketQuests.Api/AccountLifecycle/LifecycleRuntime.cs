using HoneyDrunk.Data.Outbox.Dispatcher.Registration;
using HoneyDrunk.Data.Outbox.Registration;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using HoneyDrunk.Transport.AzureServiceBus.DependencyInjection;
using HoneyDrunk.Transport.DependencyInjection;
using PocketQuests.Data.Context;

namespace PocketQuests.Api.AccountLifecycle;

/// <summary>Private managed-identity lifecycle delivery; no deletion receiver is publicly exposed.</summary>
public static class LifecycleRuntime
{
    /// <summary>Composes published Data/Transport nodes when the private broker is configured.</summary>
    /// <param name="builder">Product host.</param>
    public static void AddLifecycleRuntime(this WebApplicationBuilder builder)
    {
        var bus = builder.Configuration["Lifecycle:ServiceBusNamespace"];
        if (string.IsNullOrWhiteSpace(bus))
            return;
        var queue = builder.Configuration["Lifecycle:ConsumerQueue"] ?? throw new InvalidOperationException("Private lifecycle queue is required.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["Lifecycle:AcknowledgmentQueue"]))
            throw new InvalidOperationException("Private acknowledgment queue is required.");
        builder.Services.AddSingleton<Azure.Core.TokenCredential>(new Azure.Identity.ManagedIdentityCredential(Azure.Identity.ManagedIdentityId.SystemAssigned));
        builder.Services.AddHoneyDrunkDataOutbox<QuestDbContext>();
        builder.Services.AddHoneyDrunkServiceBusTransportWithManagedIdentity(bus, queue, options =>
        {
            // Complete only after the lifecycle handler commits its state and acknowledgment.
            options.AutoComplete = false;
            options.BlobFallback.Enabled = false;
        });
        builder.Services.AddOptions<AzureServiceBusOptions>()
            .Validate(
                options => !options.AutoComplete && !options.BlobFallback.Enabled,
                "Lifecycle delivery requires manual settlement and broker-confirmed publishing.")
            .ValidateOnStart();
        builder.Services.AddSingleton<ITransportPublisher, LifecycleAckPublisher>();
        builder.Services.AddMessageHandler<LifecycleIntent, LifecycleIntentHandler>();
        builder.Services.AddOutboxDispatcher();
        builder.Services.AddHostedService<LifecycleMaintenance>();
    }
}
