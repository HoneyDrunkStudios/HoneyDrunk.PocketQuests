using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Domain.Services.Lifecycle;

/// <summary>Retains ErasureMarker ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="accountData">Scoped IAccountDataService dependency.</param>
public sealed class ErasureMarkerService(IErasureMarkerDataService data, IAccountDataService accountData) : IErasureMarkerService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ErasureMarkerEntity>> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default) =>
        data.GetByIdentityUserIdAsync(identityUserId, cancellationToken);

    /// <inheritdoc />
    public async Task<ErasureMarkerEntity> SaveAsync(ErasureMarkerEntity value, CancellationToken cancellationToken = default)
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

        if (original.Id != current.Id)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (original.CreatedAt != value.CreatedAt)
            throw new InvalidOperationException("The original verified erasure time must be retained.");

        if (current.Id != value.Id)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task PurgeAsync(string identityUserId, DateTimeOffset originalErasedAt, DateTimeOffset now, CancellationToken token = default)
    {
        if (originalErasedAt > now || originalErasedAt <= now.AddDays(-35))
            throw new ArgumentException("A current verified marker is required.");
        await accountData.DeleteOwnedAsync(identityUserId, token);
        if (await data.FindByIdAsync(identityUserId, token) is null)
            await SaveAsync(new ErasureMarkerEntity { Id = identityUserId, CreatedAt = originalErasedAt.ToUniversalTime() }, token);
    }

    /// <inheritdoc />
    public Task PruneAsync(DateTimeOffset now, CancellationToken token = default) => data.DeleteExpiredAsync(now.AddDays(-35), token);
}
