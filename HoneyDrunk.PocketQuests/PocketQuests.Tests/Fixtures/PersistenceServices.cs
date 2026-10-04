using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Services;
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
        services.AddQuestBusinessServices();
        services.AddScoped<QuestStore>();
        services.AddScoped<IQuestStore>(serviceProvider => serviceProvider.GetRequiredService<QuestStore>());
        services.AddScoped<ISyncAnchors>(serviceProvider => serviceProvider.GetRequiredService<QuestStore>());
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        while (scopes.TryTake(out var scope))
            await scope.DisposeAsync();
        await provider.DisposeAsync();
    }

    internal AsyncServiceScope CreateScope() => provider.CreateAsyncScope();

    internal T Resolve<T>()
        where T : notnull
    {
        var scope = provider.CreateAsyncScope();
        scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
