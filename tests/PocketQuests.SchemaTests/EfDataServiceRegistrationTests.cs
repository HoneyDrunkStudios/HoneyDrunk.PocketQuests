using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Tests.Fixtures;

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

        Assert.Same(first.ServiceProvider.GetRequiredService<AppDbContext>(), first.ServiceProvider.GetRequiredService<BaseDbContext>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<AppDbContext>(), second.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Real feature contracts resolve with scoped Data and the Domain assembly has no persistence dependencies.</summary>
    [Fact]
    public void FeatureServicesResolveWithScopedDependenciesAndDomainRemainsPure()
    {
        var services = new ServiceCollection();
        services.AddQuestDataServices("Server=(localdb)\\unused;Database=unused;Integrated Security=true");
        PersistenceServices.Register(services);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var contracts = typeof(PocketQuests.Services.Quests.IQuestService).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace?.StartsWith("PocketQuests.Services.", StringComparison.Ordinal) == true).ToArray();
        Assert.Contains(typeof(PocketQuests.Services.Lifecycle.ILifecycleService), contracts);
        Assert.Contains(typeof(PocketQuests.Services.Profiles.IProfileService), contracts);
        foreach (var contract in contracts)
        {
            Assert.Same(first.ServiceProvider.GetRequiredService(contract), first.ServiceProvider.GetRequiredService(contract));
            Assert.NotSame(first.ServiceProvider.GetRequiredService(contract), second.ServiceProvider.GetRequiredService(contract));
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(contract));
        }

        Assert.Same(first.ServiceProvider.GetRequiredService<PocketQuests.Services.Quests.QuestService>(), first.ServiceProvider.GetRequiredService<PocketQuests.Services.Quests.IQuestService>());
        Assert.Same(first.ServiceProvider.GetRequiredService<PocketQuests.Services.Lifecycle.LifecycleService>(), first.ServiceProvider.GetRequiredService<PocketQuests.Services.Lifecycle.ILifecycleService>());
        var domain = typeof(PocketQuests.Domain.Quests.Aggregates.QuestAggregate).Assembly;
        Assert.DoesNotContain(domain.GetReferencedAssemblies(), assembly => assembly.Name?.Contains("Data", StringComparison.Ordinal) == true || assembly.Name?.Contains("EntityFramework", StringComparison.Ordinal) == true || assembly.Name?.Contains("Identity", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(domain.GetTypes(), type => type.Namespace?.StartsWith("PocketQuests.Domain.Services.", StringComparison.Ordinal) == true);
    }
}
