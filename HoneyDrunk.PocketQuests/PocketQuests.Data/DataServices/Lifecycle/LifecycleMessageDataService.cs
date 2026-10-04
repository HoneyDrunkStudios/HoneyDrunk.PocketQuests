using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.DataServices.Lifecycle;

/// <summary>EF persistence and queries for LifecycleMessage.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class LifecycleMessageDataService(QuestDbContext context) : BaseDataService<LifecycleMessageEntity>(context), ILifecycleMessageDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LifecycleMessageEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task AddWithEnvelopeAsync(LifecycleMessageEntity ownership, HoneyDrunk.Data.Outbox.OutboxMessage message, CancellationToken token = default)
    {
        await Context.Set<HoneyDrunk.Data.Outbox.OutboxMessage>().AddAsync(message, token);
        await AddAsync(ownership, token);
    }

    /// <inheritdoc />
    public async Task DeleteDeliveredOrExpiredAsync(DateTimeOffset now, CancellationToken token = default)
    {
        if (Context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Owned envelope retention requires a transaction.");
        var ids = await DbSet.Where(link => link.ExpiresAt <= now || Context.Set<HoneyDrunk.Data.Outbox.OutboxMessage>()
            .Any(message => message.Id == link.OutboxMessageId && message.Status == HoneyDrunk.Data.Outbox.OutboxMessageStatus.Dispatched))
            .Select(link => link.OutboxMessageId).ToArrayAsync(token);
        await DbSet.Where(link => ids.Contains(link.OutboxMessageId)).ExecuteDeleteAsync(token);
        await Context.Set<HoneyDrunk.Data.Outbox.OutboxMessage>().Where(message => ids.Contains(message.Id)).ExecuteDeleteAsync(token);
    }
}
