using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using System.Text.Json;

namespace PocketQuests.Services.Lifecycle.Mapping;

internal static class AcknowledgmentMapping
{
    internal static OutboxMessage ToEnvelope(this LifecycleIntent intent, Guid id, string queue, DateTimeOffset now) => new()
    {
        Id = id,
        Type = typeof(LifecycleAck).AssemblyQualifiedName!,
        Payload = JsonSerializer.Serialize(new LifecycleAck(intent.UserId, intent.Version, IdentityProtocol.ConsumerId, intent.Acknowledgment)),
        Headers = JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxHeaderNames.Destination] = queue }),
        OccurredAt = now,
        TenantId = "internal",
        CorrelationId = id.ToString("D"),
        Status = OutboxMessageStatus.Pending,
        RetryCount = 0,
    };

    internal static LifecycleMessageEntity ToOwnership(this LifecycleIntent intent, Guid id, Guid? accountId, DateTimeOffset expiresAt, DateTimeOffset now) => new()
    {
        Id = id,
        IdentityUserId = intent.UserId,
        AccountId = accountId,
        LifecycleVersion = intent.Version,
        OutboxMessageId = id,
        ExpiresAt = expiresAt,
        CreatedAt = now,
    };
}
