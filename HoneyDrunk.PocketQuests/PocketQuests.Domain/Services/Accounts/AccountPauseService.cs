using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Retains AccountPause ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class AccountPauseService(IAccountPauseDataService data) : IAccountPauseService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AccountPauseEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountPauseEntity> SaveAsync(Guid accountId, AccountPauseEntity value, CancellationToken cancellationToken = default)
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
            || original.ScopeCode != current.ScopeCode
            || original.CategoryId != current.CategoryId
            || original.StartedAt != current.StartedAt
            || original.CommandReceiptId != current.CommandReceiptId
            || original.CreationOrdinal != current.CreationOrdinal)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (original.EndedAt is not null && original.EndedAt != current.EndedAt)
            throw new InvalidOperationException("A completed pause interval cannot be reopened or rewritten.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.ScopeCode != value.ScopeCode
            || current.CategoryId != value.CategoryId
            || current.StartedAt != value.StartedAt
            || current.CommandReceiptId != value.CommandReceiptId
            || current.CreationOrdinal != value.CreationOrdinal)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.EndedAt is not null && current.EndedAt != value.EndedAt)
            throw new InvalidOperationException("A completed pause interval cannot be reopened or rewritten.");

        current.EndedAt = value.EndedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplyPausesAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var now = change.Now;

        for (var index = 0; index < aggregate.Schedule.Pauses.Length; index++)
        {
            var pause = aggregate.Schedule.Pauses[index];
            var id = QuestValues.Derived(account.Id, $"pause/{index}/{pause.CategoryId}/{pause.StartedAt:O}");
            var prior = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(p => p.Id == id);
            await SaveAsync(
                account.Id,
                new AccountPauseEntity
                {
                    Id = id,
                    AccountId = account.Id,
                    ScopeCode = "Category",
                    CreationOrdinal = index + 1,
                    CategoryId = pause.CategoryId,
                    StartedAt = pause.StartedAt,
                    EndedAt = pause.EndedAt,
                    CommandReceiptId = prior is null ? change.ReceiptId : prior.CommandReceiptId,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
        }
    }
}
