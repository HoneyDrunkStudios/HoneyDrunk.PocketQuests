using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.AzureServiceBus;
using HoneyDrunk.Transport.AzureServiceBus.Configuration;
using Microsoft.Extensions.Options;

namespace PocketQuests.Api.AccountLifecycle;

/// <summary>Routes published Transport acknowledgments to the fixed private queue with finite broker TTL.</summary>
public sealed class LifecycleAckPublisher : ITransportPublisher, IAsyncDisposable
{
    private readonly ServiceBusTransportPublisher publisher;
    private readonly string queue;
    private readonly TimeProvider clock;

    /// <summary>Initializes a new instance of the <see cref="LifecycleAckPublisher"/> class.</summary>
    /// <param name="client">Managed-identity broker client.</param>
    /// <param name="configuration">Trusted environment configuration.</param>
    /// <param name="logger">Transport logging.</param>
    /// <param name="clock">Authoritative clock.</param>
    public LifecycleAckPublisher(Azure.Messaging.ServiceBus.ServiceBusClient client, IConfiguration configuration, ILogger<ServiceBusTransportPublisher> logger, TimeProvider clock)
    {
        this.clock = clock;
        queue = configuration["Lifecycle:AcknowledgmentQueue"] ?? throw new InvalidOperationException("Acknowledgment queue is missing.");

        // A failed broker send must remain retryable in SQL; Blob persistence is not delivery.
        publisher = new(client, Options.Create(new AzureServiceBusOptions { Address = queue, BlobFallback = new() { Enabled = false } }), logger);
    }

    /// <inheritdoc />
    public Task PublishAsync(ITransportEnvelope envelope, IEndpointAddress destination, CancellationToken cancellationToken = default)
    {
        var remaining = envelope.Timestamp.AddHours(1) - clock.GetUtcNow();
        if (destination.Address != queue || remaining <= TimeSpan.Zero)
            throw new InvalidOperationException("Acknowledgment destination is invalid or the envelope expired.");
        return publisher.PublishAsync(envelope, EndpointAddress.Create(queue, queue, timeToLive: remaining), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync(IEnumerable<ITransportEnvelope> envelopes, IEndpointAddress destination, CancellationToken cancellationToken = default)
    {
        foreach (var envelope in envelopes)
            await PublishAsync(envelope, destination, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => publisher.DisposeAsync();
}
