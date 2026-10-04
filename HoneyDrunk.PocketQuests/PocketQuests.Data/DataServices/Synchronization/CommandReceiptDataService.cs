using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Synchronization;

/// <summary>EF persistence and queries for CommandReceipt.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CommandReceiptDataService(QuestDbContext context) : BaseDataService<CommandReceiptEntity>(context), ICommandReceiptDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandReceiptEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
