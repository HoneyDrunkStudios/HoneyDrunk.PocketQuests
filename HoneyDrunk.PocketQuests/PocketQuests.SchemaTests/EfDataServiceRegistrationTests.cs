using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Services;
using PocketQuests.Domain.Services.Lifecycle;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.SchemaTests;

/// <summary>Checks the actual Data registrations without opening a SQL connection.</summary>
public sealed class EfDataServiceRegistrationTests
{
    /// <summary>Every entity service shares its scoped context and stays isolated from other scopes.</summary>
    [Fact]
    public void AllEntityDataServicesAndGenericBaseResolveAsScoped()
    {
        var services = new ServiceCollection();
        services.AddQuestDataServices("Server=(localdb)\\unused;Database=unused;Integrated Security=true");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var interfaces = typeof(IBaseDataService<>).Assembly.GetTypes()
            .Where(type => type.IsInterface && !type.IsGenericType && type.GetInterfaces().Any(parent => parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(IBaseDataService<>)))
            .ToArray();
        Assert.Equal(33, interfaces.Length);
        foreach (var contract in interfaces.Append(typeof(IBaseDataService<AccountEntity>)))
        {
            Assert.Same(first.ServiceProvider.GetRequiredService(contract), first.ServiceProvider.GetRequiredService(contract));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(contract), second.ServiceProvider.GetRequiredService(contract));
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(contract));
        }

        Assert.Same(first.ServiceProvider.GetRequiredService<QuestDbContext>(), first.ServiceProvider.GetRequiredService<BaseDbContext>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<QuestDbContext>(), second.ServiceProvider.GetRequiredService<QuestDbContext>());
    }

    /// <summary>Actual Domain registrations and Application aliases resolve only inside scopes.</summary>
    [Fact]
    public void BusinessServicesAndApplicationAdaptersResolveWithTheirScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddQuestDataServices("Server=(localdb)\\unused;Database=unused;Integrated Security=true");
        services.AddQuestBusinessServices();
        services.AddScoped<QuestStore>();
        services.AddScoped<IQuestStore>(provider => provider.GetRequiredService<QuestStore>());
        services.AddScoped<ISyncAnchors>(provider => provider.GetRequiredService<QuestStore>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var contracts = typeof(IQuestService).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace?.StartsWith("PocketQuests.Domain.Services.", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(35, contracts.Length);
        foreach (var contract in contracts.Concat([typeof(IQuestStore), typeof(ISyncAnchors)]))
        {
            Assert.Same(first.ServiceProvider.GetRequiredService(contract), first.ServiceProvider.GetRequiredService(contract));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(contract), second.ServiceProvider.GetRequiredService(contract));
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(contract));
        }

        Assert.Same(first.ServiceProvider.GetRequiredService<QuestStore>(), first.ServiceProvider.GetRequiredService<IQuestStore>());
        Assert.Same(first.ServiceProvider.GetRequiredService<QuestStore>(), first.ServiceProvider.GetRequiredService<ISyncAnchors>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IAccountLifecycleStateService>(), first.ServiceProvider.GetRequiredService<IQuestLifecycle>());
    }
}
