using HoneyDrunk.Data.Outbox;
using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Models.Accounts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Retains LifecycleMessage ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="accountData">Scoped IAccountDataService dependency.</param>
public sealed class LifecycleMessageService(ILifecycleMessageDataService data, IAccountDataService accountData) : ILifecycleMessageService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<LifecycleMessageEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<LifecycleMessageEntity> SaveAsync(LifecycleMessageEntity value, CancellationToken cancellationToken = default)
    {
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.IdentityUserId != current.IdentityUserId
            || original.AccountId != current.AccountId
            || original.LifecycleVersion != current.LifecycleVersion
            || original.OutboxMessageId != current.OutboxMessageId
            || original.ExpiresAt != current.ExpiresAt)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.IdentityUserId != value.IdentityUserId
            || current.AccountId != value.AccountId
            || current.LifecycleVersion != value.LifecycleVersion
            || current.OutboxMessageId != value.OutboxMessageId
            || current.ExpiresAt != value.ExpiresAt)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(LifecycleIntent intent, string queue, DateTimeOffset now, CancellationToken token = default)
    {
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{intent.UserId}/{intent.Version}/{intent.Acknowledgment}")).AsSpan(0, 16));
        var prior = await data.FindByIdAsync(id, token);
        if (prior is not null)
        {
            if (prior.IdentityUserId != intent.UserId || prior.LifecycleVersion != intent.Version)
                throw new InvalidOperationException("Acknowledgment identifier belongs to another owner or version.");
            return;
        }

        var account = await accountData.GetByIdentityUserIdAsync(intent.UserId, token);
        var acknowledgment = new LifecycleAck(intent.UserId, intent.Version, IdentityProtocol.ConsumerId, intent.Acknowledgment);
        var envelope = new OutboxMessage
        {
            Id = id,
            Type = typeof(LifecycleAck).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(acknowledgment),
            Headers = JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxHeaderNames.Destination] = queue }),
            OccurredAt = now,
            TenantId = "internal",
            CorrelationId = id.ToString("D"),
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };
        var ownership = new LifecycleMessageEntity
        {
            Id = id,
            IdentityUserId = intent.UserId,
            AccountId = account?.Id,
            LifecycleVersion = intent.Version,
            OutboxMessageId = id,
            ExpiresAt = (intent.ExpiresAt < now.AddHours(1) ? intent.ExpiresAt : now.AddHours(1)).ToUniversalTime(),
            CreatedAt = now
        };
        await data.AddWithEnvelopeAsync(ownership, envelope, token);
    }

    /// <inheritdoc />
    public Task PruneAsync(DateTimeOffset now, CancellationToken token = default) => data.DeleteDeliveredOrExpiredAsync(now, token);
}
