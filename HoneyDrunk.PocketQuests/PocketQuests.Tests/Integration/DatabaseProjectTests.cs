using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Tests.Fixtures;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Validates DACPAC upgrade and repeat deployment against real SQL Server.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>An original ledger upgrades without losing history, and repeat publishing is safe.</summary>
    /// <returns>The completed regression.</returns>
    [Fact]
    public async Task DatabaseProjectBackfillsLegacyLedgerAndPreservesHistoryOnRepublish()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        var legacyName = "LegacyQuestLedger_" + Guid.NewGuid().ToString("N");
        var legacyPath = Path.Combine(AppContext.BaseDirectory, legacyName + ".dacpac");
        try
        {
            using var model = new TSqlModel(SqlServerVersion.Sql150, new TSqlModelOptions());
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyQuestLedger.sql"));
            model.AddObjects(script);
            DacPackageExtensions.BuildPackage(legacyPath, model, new PackageMetadata { Name = legacyName, Version = "1.0.0.0" });
            await DatabaseSchema.DeployAsync(db.Database, legacyName);
        }
        finally
        {
            File.Delete(legacyPath);
        }

        var account = Guid.NewGuid();
        var occurrence = Guid.NewGuid();
        var completion = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var snapshot = JsonSerializer.Serialize(Catalog.Quests[6]);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Accounts (Id,IdentityKey,Zone,CreatedAt) VALUES ({account},{new string('A', 64)},{"UTC"},{now})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Occurrences (AccountId,Id,QuestSnapshot,AcceptedAt) VALUES ({account},{occurrence},{snapshot},{now})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Completions (AccountId,Id,OccurrenceId,RecordedAt) VALUES ({account},{completion},{occurrence},{now})");

        await DatabaseSchema.DeployAsync(db.Database);
        Assert.Equal(snapshot, (await db.Completions.SingleAsync()).QuestSnapshot);
        Assert.Null((await db.Accounts.SingleAsync()).Profile);
        await DatabaseSchema.DeployAsync(db.Database);
        db.ChangeTracker.Clear();
        Assert.Equal(snapshot, (await db.Completions.SingleAsync()).QuestSnapshot);
        Assert.Single(await db.Accounts.ToListAsync());
        Assert.Single(await db.Occurrences.ToListAsync());
    }
}
