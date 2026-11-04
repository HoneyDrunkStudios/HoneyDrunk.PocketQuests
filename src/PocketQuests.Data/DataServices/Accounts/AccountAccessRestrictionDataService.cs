using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Administration;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF reads and staged moderation writes sharing the account transaction.</summary>
/// <param name="context">The shared scoped transaction context.</param>
public sealed class AccountAccessRestrictionDataService(AppDbContext context)
    : BaseDataService<AccountAccessRestrictionEntity>(context), IAccountAccessRestrictionDataService
{
    private IQueryable<AccountAccessRestrictionEntity> Restrictions => DbSet;

    /// <inheritdoc />
    public Task<AccountAccessRestrictionEntity?> GetForAccount(Guid accountId, CancellationToken token) =>
        Restrictions.SingleOrDefaultAsync(row => row.AccountId == accountId, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserRow>> Search(string query, string? after, int limit, CancellationToken token)
    {
        var accounts = Context.Account.AsNoTracking()
            .Where(account => !Context.ErasureMarker.Any(marker => marker.Id == account.IdentityUserId));
        if (query.Length > 0)
        {
            accounts = accounts.Where(account => account.IdentityUserId.Contains(query)
                || (account.CharacterName != null && EF.Functions.Collate(account.CharacterName, "Latin1_General_100_CI_AI").Contains(query)));
        }

        if (after is not null)
            accounts = accounts.Where(account => account.IdentityUserId.CompareTo(after) > 0);
        return await (from account in accounts
                      join restriction in Restrictions.AsNoTracking() on account.Id equals restriction.AccountId into states
                      from restriction in states.DefaultIfEmpty()
                      orderby account.IdentityUserId
                      select new AdminUserRow(account.IdentityUserId, account.CharacterName, account.TimeZoneId, account.IsOnboardingComplete, account.CreatedAt, restriction != null && restriction.IsSuspended, restriction == null ? null : restriction.Reason, restriction == null ? 0 : restriction.Revision))
            .Take(limit).ToListAsync(token);
    }
}
