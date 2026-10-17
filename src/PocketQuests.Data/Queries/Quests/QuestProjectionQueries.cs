using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.Queries.Quests;

internal static class QuestProjectionQueries
{
    internal static async Task<QuestProjectionRows> Read(AppDbContext context, Guid accountId, CancellationToken token) => new()
    {
            Ledger = await context.XpLedgerEntry.Where(row => row.AccountId == accountId).ToListAsync(token),
            Balances = await context.XpBalance.Where(row => row.AccountId == accountId).ToListAsync(token),
            Entitlements = await context.AccountEntitlement.Where(row => row.AccountId == accountId).ToListAsync(token),
    };
}
