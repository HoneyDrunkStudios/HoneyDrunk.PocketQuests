using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for XpLedgerEntry.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class XpLedgerEntryDataService(AppDbContext context) : BaseDataService<XpLedgerEntryEntity>(context), IXpLedgerEntryDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<XpLedgerEntryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
