using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.DataServices.Accounts;

/// <summary>EF persistence and queries for AccountAuditRecord.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AccountAuditRecordDataService(AppDbContext context) : BaseDataService<AccountAuditRecordEntity>(context), IAccountAuditRecordDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountAuditRecordEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddWithAudit(AccountAuditRecordEntity ownership, HoneyDrunk.Audit.Data.AuditRecord audit, CancellationToken token = default)
    {
        await Context.Set<HoneyDrunk.Audit.Data.AuditRecord>().AddAsync(audit, token);
        await AddAsync(ownership, token);
    }
}
