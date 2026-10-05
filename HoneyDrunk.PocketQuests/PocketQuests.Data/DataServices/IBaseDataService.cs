using HoneyDrunk.Data.Abstractions.Repositories;

namespace PocketQuests.Data.DataServices;

/// <summary>Standard tracked EF reads and staged CRUD operations; the business transaction owns SaveChanges.</summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
public interface IBaseDataService<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    /// <summary>Copies EF's original tracked values for business checks against in-place edits.</summary>
    /// <param name="entity">An entity tracked by this scope.</param>
    /// <returns>The original values captured when EF started tracking the row.</returns>
    TEntity GetOriginalValues(TEntity entity);

    /// <summary>Runs one caller-selected atomic operation; rejects nesting and unrelated pending changes.</summary>
    /// <typeparam name="TResult">The caller's result.</typeparam>
    /// <param name="operation">Reads and staged writes belonging to this transaction.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The result only after a successful commit.</returns>
    Task<TResult> ExecuteInTransaction<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken token = default);
}
