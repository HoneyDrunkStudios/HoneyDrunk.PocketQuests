using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Tests.Integration;

/// <summary>Fresh schema and safe repeat publication against the same disposable product database.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>The initial schema has only canonical product tables plus unchanged shared dependencies.</summary>
    /// <returns>Completion after repeat deployment and persisted-history checks.</returns>
    [Fact]
    public async Task FreshCanonicalSchemaAndRepeatedPublicationPreserveCommittedHistory()
    {
        var identity = TestIdentity("publication-owner");
        var now = DateTimeOffset.UtcNow;
        await Store().Initialize(identity, "UTC", now, default);
        var command = new PocketQuests.Domain.Models.Quests.QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07");
        var original = await ExecuteAt(identity, command, now);
        await Republish();
        await Republish();
        await using var db = Context();
        Assert.Equal(1, await db.Read.Set<AccountEntity>().CountAsync());
        Assert.Equal(1, await db.Read.Set<QuestOccurrenceEntity>().CountAsync());
        Assert.Equal(1, await db.Read.Set<CommandReceiptEntity>().CountAsync());
        var tables = await db.Database.SqlQueryRaw<string>("SELECT SCHEMA_NAME(schema_id)+'.'+name AS Value FROM sys.tables WHERE is_ms_shipped=0").ToListAsync();
        Assert.Equal(new[] { "dbo.AuditRecords", "outbox.OutboxMessages" }, tables.Where(t => !t.StartsWith("pocketquests.", StringComparison.Ordinal)).Order().ToArray());
        Assert.Equal(33, tables.Count(t => t.StartsWith("pocketquests.", StringComparison.Ordinal)));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(await ExecuteAt(identity, command, now.AddHours(1))));
    }
}
