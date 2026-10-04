using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Domain.Services.Synchronization;

/// <summary>Retains CommandReceipt ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class CommandReceiptService(ICommandReceiptDataService data) : ICommandReceiptService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<CommandReceiptEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<CommandReceiptEntity> SaveAsync(Guid accountId, CommandReceiptEntity value, CancellationToken cancellationToken = default)
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
            || original.CommandType != current.CommandType
            || original.ApiVersion != current.ApiVersion
            || !original.PayloadDigest.AsSpan().SequenceEqual(current.PayloadDigest)
            || original.DigestVersion != current.DigestVersion
            || original.OutcomeVersion != current.OutcomeVersion
            || original.OutcomeJson != current.OutcomeJson
            || original.AppliedMutationVersion != current.AppliedMutationVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.CommandType != value.CommandType
            || current.ApiVersion != value.ApiVersion
            || !current.PayloadDigest.AsSpan().SequenceEqual(value.PayloadDigest)
            || current.DigestVersion != value.DigestVersion
            || current.OutcomeVersion != value.OutcomeVersion
            || current.OutcomeJson != value.OutcomeJson
            || current.AppliedMutationVersion != value.AppliedMutationVersion)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }
}
