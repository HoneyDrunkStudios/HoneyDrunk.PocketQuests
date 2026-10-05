using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for XpLedgerEntry.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class XpLedgerEntryDataService(AppDbContext context) : BaseDataService<XpLedgerEntryEntity>(context), IXpLedgerEntryDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<XpLedgerEntryEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
