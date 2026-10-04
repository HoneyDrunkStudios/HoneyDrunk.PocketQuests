using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Retains TimeZoneChange ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class TimeZoneChangeService(ITimeZoneChangeDataService data) : ITimeZoneChangeService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<TimeZoneChangeEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<TimeZoneChangeEntity> SaveAsync(Guid accountId, TimeZoneChangeEntity value, CancellationToken cancellationToken = default)
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
            || original.FromTimeZoneId != current.FromTimeZoneId
            || original.ToTimeZoneId != current.ToTimeZoneId
            || original.EffectiveAt != current.EffectiveAt
            || original.CommandReceiptId != current.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.FromTimeZoneId != value.FromTimeZoneId
            || current.ToTimeZoneId != value.ToTimeZoneId
            || current.EffectiveAt != value.EffectiveAt
            || current.CommandReceiptId != value.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task RecordChangeAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        if (command.Action == QuestActions.Zone && aggregate.Zone != change.TimeZoneBefore)
        {
            await SaveAsync(
                account.Id,
                new TimeZoneChangeEntity
                {
                    Id = command.OperationId,
                    AccountId = account.Id,
                    FromTimeZoneId = change.TimeZoneBefore,
                    ToTimeZoneId = aggregate.Zone,
                    EffectiveAt = recordedAt,
                    CommandReceiptId = command.OperationId,
                    CreatedAt = now
                },
                token);
        }
    }
}
