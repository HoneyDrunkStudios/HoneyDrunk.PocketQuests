using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Retains AccountInterest ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class AccountInterestService(IAccountInterestDataService data) : IAccountInterestService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AccountInterestEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountInterestEntity> SaveAsync(Guid accountId, AccountInterestEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(new object[] { value.AccountId, value.CategoryId }, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.AccountId != current.AccountId
            || original.CategoryId != current.CategoryId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.AccountId != value.AccountId
            || current.CategoryId != value.CategoryId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.Position = value.Position;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplyInterestsAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var now = change.Now;

        var profile = change.Aggregate.Profile;
        foreach (var interest in profile.Interests)
            await SaveAsync(account.Id, new AccountInterestEntity { AccountId = account.Id, CategoryId = interest, Position = profile.Interests.IndexOf(interest), CreatedAt = now }, token);

        data.RemoveRange((await data.GetByAccountIdAsync(account.Id, token)).Where(row => !profile.Interests.Contains(row.CategoryId)));
    }
}
