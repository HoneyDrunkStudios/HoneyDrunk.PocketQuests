using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data.DataServices;
using PocketQuests.Services;
using PocketQuests.Services.Accounts;
using System.Collections.Concurrent;

namespace PocketQuests.Tests.Fixtures;

/// <summary>Owns real scoped persistence services for disposable-database tests; contains no workflow implementation.</summary>
internal sealed class PersistenceServices : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly ConcurrentBag<AsyncServiceScope> scopes = [];

    internal PersistenceServices(string connection)
    {
        var services = new ServiceCollection();
        services.AddQuestDataServices(connection);
        Register(services);
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        while (scopes.TryTake(out var scope))
            await scope.DisposeAsync();
        await provider.DisposeAsync();
    }

    internal static void Register(IServiceCollection services)
    {
        services.AddScoped<ICurrentAccount>(_ => new PrivateTestAccount());
        services.AddQuestServices();
        services.AddScoped<TestQuestWorkflow>(provider => new TestQuestWorkflow(
            provider.GetRequiredService<PocketQuests.Services.Quests.QuestService>(),
            provider.GetRequiredService<PocketQuests.Services.Profiles.ProfileService>(),
            provider.GetRequiredService<PocketQuests.Services.Synchronization.SynchronizationService>(),
            provider.GetRequiredService<PocketQuests.Services.Reconciliation.ReconciliationService>(),
            provider.GetRequiredService<PocketQuests.Services.Exports.ExportService>(),
            provider.GetRequiredService<PocketQuests.Services.Quests.OccurrenceReadService>()));
    }

    internal AsyncServiceScope CreateScope() => provider.CreateAsyncScope();

    internal T Resolve<T>()
        where T : notnull
    {
        var scope = provider.CreateAsyncScope();
        scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    private sealed class PrivateTestAccount : ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity => throw new InvalidOperationException("SQL workflows pass their verified identity explicitly.");
    }
}
