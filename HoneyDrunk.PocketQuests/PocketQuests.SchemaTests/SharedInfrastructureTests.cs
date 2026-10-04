using HoneyDrunk.Data.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Api.Quests;
using PocketQuests.Data;

namespace PocketQuests.SchemaTests;

/// <summary>Verifies shared Outbox SQL composition survives removal of the old product context.</summary>
/// <param name="fixture">Isolated product database.</param>
public sealed class SharedInfrastructureTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>The actual lifecycle registration claims and completes a committed acknowledgment through shared Data.</summary>
    /// <returns>Completion after persisted dispatcher state and model checks.</returns>
    [Fact]
    public async Task CanonicalLifecycleOutboxUsesSharedInfrastructureMappings()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:quests"] = fixture.Connection,
            ["Lifecycle:ServiceBusNamespace"] = "synthetic.invalid",
            ["Lifecycle:ConsumerQueue"] = "private-intents",
            ["Lifecycle:AcknowledgmentQueue"] = "private-acks",
        });
        builder.AddQuestPersistence();
        builder.AddLifecycleRuntime();
        await using var services = builder.Services.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestDbContext>();
        Assert.Equal(new[] { "dbo.AuditRecords", "outbox.OutboxMessages" }, context.Model.GetEntityTypes()
            .Where(e => e.GetSchema() != "pocketquests")
            .Select(e => (e.GetSchema() ?? "dbo") + "." + e.GetTableName()).Order().ToArray());
        Assert.Equal(33, context.Model.GetEntityTypes().Count(e => e.GetSchema() == "pocketquests"));

        var now = DateTimeOffset.UtcNow;
        var owner = "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant();
        var lifecycle = fixture.Lifecycle();
        await lifecycle.ReceiveLifecycle(new(owner, 1, "Inactive", now, now.AddHours(1), "pocketquests", new string('a', 64), now), "private-acks", now);
        var reader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var claimed = Assert.Single(await reader.ClaimBatchAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(OutboxMessageStatus.Leased, claimed.Status);
        await reader.MarkDispatchedAsync(claimed.Id);
        var persisted = await context.Set<OutboxMessage>().AsNoTracking().SingleAsync();
        Assert.Equal(OutboxMessageStatus.Dispatched, persisted.Status);
        Assert.Empty(await reader.ClaimBatchAsync(10, TimeSpan.FromMinutes(1)));
    }
}
