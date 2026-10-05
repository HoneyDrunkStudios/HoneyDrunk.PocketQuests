using HoneyDrunk.Data.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.DataServices;

/// <summary>Reuses HoneyDrunk.Data's EF repository for standard reads and staged insert/update/delete operations.</summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
/// <param name="context">The scoped context shared by every data service in the transaction.</param>
public class BaseDataService<TEntity>(AppDbContext context) : EfRepository<TEntity, AppDbContext>(context), IBaseDataService<TEntity>
    where TEntity : class
{
    private readonly HashSet<Guid> loadedAccounts = [];
    private Guid? loadedTransactionId;

    /// <inheritdoc />
    public TEntity GetOriginalValues(TEntity entity) => (TEntity)Context.Entry(entity).OriginalValues.ToObject();

    /// <inheritdoc />
    public override ValueTask<TEntity?> FindByIdAsync(object id, CancellationToken cancellationToken = default) =>
        id is object[] keys ? DbSet.FindAsync(keys, cancellationToken) : base.FindByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransaction<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken token = default)
    {
        if (Context.Database.CurrentTransaction is not null || Context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("The operation requires its own transaction and no unrelated pending changes.");
        Context.ChangeTracker.Clear();
        try
        {
            await using var transaction = await Context.Database.BeginTransactionAsync(token);
            var result = await operation(token);
            if (Context.ChangeTracker.HasChanges())
                await Context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return result;
        }
        finally
        {
            // The entry guard established exclusive ownership of this scope's staged work.
            // Never retry a possibly committed command here; its receipt resolves the next request.
            Context.ChangeTracker.Clear();
        }
    }

    /// <summary>Loads an owned collection once in the current explicit transaction; callers then read the tracked local view.</summary>
    /// <param name="accountId">Resolved account identity.</param>
    /// <param name="query">The full account-filtered entity query.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion once persisted rows are tracked.</returns>
    protected async Task LoadAccountCollectionAsync(Guid accountId, IQueryable<TEntity> query, CancellationToken token)
    {
        var transactionId = Context.Database.CurrentTransaction?.TransactionId;
        if (transactionId is null)
        {
            await query.LoadAsync(token);
            return;
        }

        if (loadedTransactionId != transactionId)
        {
            loadedAccounts.Clear();
            loadedTransactionId = transactionId;
        }

        if (loadedAccounts.Contains(accountId))
            return;
        await query.LoadAsync(token);
        loadedAccounts.Add(accountId);
    }
}
