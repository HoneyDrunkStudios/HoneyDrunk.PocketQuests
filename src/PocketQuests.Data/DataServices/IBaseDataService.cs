using HoneyDrunk.Data.Abstractions.Repositories;
using System.Data;

namespace PocketQuests.Data.DataServices;

/// <summary>Standard tracked EF reads and staged CRUD operations; the business transaction owns SaveChanges.</summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
public interface IBaseDataService<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    /// <summary>Runs a caller-selected atomic operation with at most two transient retries before commit; rejects nesting and unrelated pending changes.</summary>
    /// <typeparam name="TResult">The caller's result.</typeparam>
    /// <param name="operation">Reads and staged writes belonging to this transaction; every attempt must re-read its state and perform no external side effects.</param>
    /// <param name="token">Cancellation.</param>
    /// <param name="isolation">Consistency required by the operation; repeatable reads protect coherent source projections.</param>
    /// <returns>The result only after a successful commit.</returns>
    Task<TResult> ExecuteInTransaction<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken token = default, IsolationLevel isolation = IsolationLevel.ReadCommitted);
}
