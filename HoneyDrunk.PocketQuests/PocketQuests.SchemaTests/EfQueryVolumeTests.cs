using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Services;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.SchemaTests;

/// <summary>Measures actual SQL command volume while retained occurrence history grows.</summary>
/// <param name="fixture">The disposable DACPAC database.</param>
public sealed class EfQueryVolumeTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>Profile commands load each account collection once and reused scopes refresh at the next transaction.</summary>
    /// <returns>Completion after SQL-count and durable-history comparisons.</returns>
    [Fact]
    public async Task ProfileCommandQueryCountDoesNotGrowPerRetainedOccurrence()
    {
        var count = 0;
        var services = new ServiceCollection();
        services.AddQuestDataServices(fixture.Connection);
        services.AddQuestBusinessServices();
        services.ConfigureDbContext<QuestDbContext>((_, options) => options.LogTo(_ => count++, [RelationalEventId.CommandExecuted]));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IQuestService>();
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        await workflow.Initialize(owner, "Etc/UTC", now);
        await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1, ExpectedRevision: 0), now);
        count = 0;
        var first = await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.FinishOnboarding), now);
        var firstCount = count;
        Assert.Single(first.Occurrences);
        await workflow.Reconcile(owner, now.AddDays(99), 100);
        count = 0;
        var later = await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.FinishOnboarding), now.AddDays(99));
        var laterCount = count;
        Assert.Equal(100, later.Occurrences.Length);
        Assert.InRange(laterCount, 1, firstCount + 10);
        Assert.InRange(laterCount, 1, 100);
        var evidence = Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_EVIDENCE");
        if (evidence is not null)
            await File.WriteAllTextAsync(Path.Combine(evidence, "ef-query-volume.json"), System.Text.Json.JsonSerializer.Serialize(new { firstCount, laterCount, firstOccurrences = 1, laterOccurrences = 100 }));
    }
}
