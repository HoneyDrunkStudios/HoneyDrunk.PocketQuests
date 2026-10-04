using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Domain.Services.Synchronization;

/// <summary>Retains SyncAnchor ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class SyncAnchorService(ISyncAnchorDataService data) : ISyncAnchorService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SyncAnchorEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<SyncAnchorEntity> SaveAsync(Guid accountId, SyncAnchorEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
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
            || original.AccountId != current.AccountId
            || original.DeviceId != current.DeviceId
            || original.BootId != current.BootId
            || original.ServerAt != current.ServerAt
            || original.DeviceAt != current.DeviceAt
            || original.RecordedTimeFloorAt != current.RecordedTimeFloorAt
            || original.IssuedMutationVersion != current.IssuedMutationVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.LastOrdinal < original.LastOrdinal || current.LastElapsedMilliseconds < original.LastElapsedMilliseconds
            || (original.InvalidatedAt is not null && original.InvalidatedAt != current.InvalidatedAt))
            throw new InvalidOperationException("Clock proof progress cannot move backwards or be reactivated.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.DeviceId != value.DeviceId
            || current.BootId != value.BootId
            || current.ServerAt != value.ServerAt
            || current.DeviceAt != value.DeviceAt
            || current.RecordedTimeFloorAt != value.RecordedTimeFloorAt
            || current.IssuedMutationVersion != value.IssuedMutationVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (value.LastOrdinal < current.LastOrdinal || value.LastElapsedMilliseconds < current.LastElapsedMilliseconds
            || (current.InvalidatedAt is not null && current.InvalidatedAt != value.InvalidatedAt))
            throw new InvalidOperationException("Clock proof progress cannot move backwards or be reactivated.");

        current.LastOrdinal = value.LastOrdinal;
        current.LastElapsedMilliseconds = value.LastElapsedMilliseconds;
        current.InvalidatedAt = value.InvalidatedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }
}
