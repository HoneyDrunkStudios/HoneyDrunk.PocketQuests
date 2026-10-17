using HoneyDrunk.Data.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace PocketQuests.Data.DataServices;

/// <summary>Reuses HoneyDrunk.Data's EF repository for standard reads and staged insert/update/delete operations.</summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
/// <param name="context">The scoped context shared by every data service in the transaction.</param>
public class BaseDataService<TEntity>(AppDbContext context) : EfRepository<TEntity, AppDbContext>(context), IBaseDataService<TEntity>
    where TEntity : class
{
    /// <inheritdoc />
    public override ValueTask<TEntity?> FindByIdAsync(object id, CancellationToken cancellationToken = default) =>
        id is object[] keys ? DbSet.FindAsync(keys, cancellationToken) : base.FindByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransaction<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken token = default, IsolationLevel isolation = IsolationLevel.ReadCommitted)
    {
        if (Context.Database.CurrentTransaction is not null || Context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("The operation requires its own transaction and no unrelated pending changes.");
        var strategy = new TransactionExecutionStrategy(Context);
        return await strategy.ExecuteAsync(Attempt, token);

        async Task<TResult> Attempt(CancellationToken cancellationToken)
        {
            Context.ChangeTracker.Clear();
            strategy.CommitStarted = false;
            try
            {
                await using var transaction = await Context.Database.BeginTransactionAsync(isolation, cancellationToken);
                var result = await operation(cancellationToken);
                if (Context.ChangeTracker.HasChanges())
                    await Context.SaveChangesAsync(cancellationToken);

                // From this point even a transient failure may hide a successful commit.
                // The next caller request must resolve the retained receipt instead of replaying here.
                strategy.CommitStarted = true;
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            finally
            {
                // Each retry re-reads state after disposing the preceding transaction.
                Context.ChangeTracker.Clear();
            }
        }
    }

    private sealed class TransactionExecutionStrategy(DbContext context)
        : SqlServerRetryingExecutionStrategy(context, 2, TimeSpan.FromSeconds(1), null)
    {
        internal bool CommitStarted { get; set; }

        protected override bool ShouldRetryOn(Exception exception) => !CommitStarted && base.ShouldRetryOn(exception);
    }
}
