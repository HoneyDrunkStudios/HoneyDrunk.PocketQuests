using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.DataServices.Progress;

/// <summary>EF persistence and queries for XpBalance.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class XpBalanceDataService(QuestDbContext context) : BaseDataService<XpBalanceEntity>(context), IXpBalanceDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<XpBalanceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
