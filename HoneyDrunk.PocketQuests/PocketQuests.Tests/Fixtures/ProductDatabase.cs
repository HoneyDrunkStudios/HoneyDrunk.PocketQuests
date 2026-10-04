using HoneyDrunk.Audit.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PocketQuests.Data;

namespace PocketQuests.Tests.Fixtures;

/// <summary>Read-only product and shared-table evidence on one explicitly supplied test database.</summary>
internal sealed class ProductDatabase(string connection) : IAsyncDisposable
{
    internal QuestDbContext Read { get; } = new(new DbContextOptionsBuilder<QuestDbContext>().UseSqlServer(connection).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);

    internal QuestDbContext Infrastructure => Read;

    internal DatabaseFacade Database => Read.Database;

    internal IQueryable<AuditRecord> Audit => Read.Set<AuditRecord>().AsNoTracking();

    public async ValueTask DisposeAsync()
    {
        await Read.DisposeAsync();
    }
}
