using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Transport.Abstractions;
using PocketQuests.Domain.Services.Lifecycle;

namespace PocketQuests.Api.AccountLifecycle;

/// <summary>Receives Identity instructions only through the private, Identity-sender-authorized endpoint.</summary>
public sealed class LifecycleIntentHandler(IQuestLifecycle lifecycle, IConfiguration configuration) : IMessageHandler<LifecycleIntent>
{
    /// <inheritdoc />
    public Task HandleAsync(LifecycleIntent message, MessageContext context, CancellationToken cancellationToken = default) =>
        lifecycle.Receive(message, configuration["Lifecycle:AcknowledgmentQueue"] ?? throw new InvalidOperationException("Acknowledgment queue is missing."), cancellationToken);
}
