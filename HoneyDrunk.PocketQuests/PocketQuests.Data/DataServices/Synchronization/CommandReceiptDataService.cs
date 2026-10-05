using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Synchronization;

/// <summary>EF persistence and queries for CommandReceipt.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CommandReceiptDataService(AppDbContext context) : BaseDataService<CommandReceiptEntity>(context), ICommandReceiptDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandReceiptEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
