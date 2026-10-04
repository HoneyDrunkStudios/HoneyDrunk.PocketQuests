using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Retains AccountEntitlement ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class AccountEntitlementService(IAccountEntitlementDataService data) : IAccountEntitlementService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AccountEntitlementEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountEntitlementEntity> SaveAsync(Guid accountId, AccountEntitlementEntity value, CancellationToken cancellationToken = default)
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
            || original.AccountId != current.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.ProfileRewardId = value.ProfileRewardId;
        current.QualifyingCount = value.QualifyingCount;
        current.IsEarned = value.IsEarned;
        current.ProjectionVersion = value.ProjectionVersion;
        current.RulesetVersion = value.RulesetVersion;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var state = change.State;
        var now = change.Now;

        foreach (var entitlement in state.Entitlements)
        {
            await SaveAsync(
                account.Id,
                new AccountEntitlementEntity
                {
                    Id = QuestValues.Derived(account.Id, "reward/" + entitlement.Id),
                    AccountId = account.Id,
                    ProfileRewardId = entitlement.Id,
                    QualifyingCount = entitlement.Count,
                    IsEarned = entitlement.Earned,
                    ProjectionVersion = change.Version,
                    RulesetVersion = "1.0",
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
        }

        data.RemoveRange((await data.GetByAccountIdAsync(account.Id, token)).Where(row => row.ProjectionVersion != change.Version));
    }
}
