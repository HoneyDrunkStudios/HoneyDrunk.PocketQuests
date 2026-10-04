using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountAuditRecord.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountAuditRecordDataService(QuestDbContext context) : BaseDataService<AccountAuditRecordEntity>(context), IAccountAuditRecordDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountAuditRecordEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task AddWithAuditAsync(AccountAuditRecordEntity ownership, HoneyDrunk.Audit.Data.AuditRecord audit, CancellationToken token = default)
    {
        await Context.Set<HoneyDrunk.Audit.Data.AuditRecord>().AddAsync(audit, token);
        await AddAsync(ownership, token);
    }
}
