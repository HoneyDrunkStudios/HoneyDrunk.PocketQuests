using PocketQuests.Api;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Data;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.SchemaTests.Architecture.Fixtures;
using PocketQuests.Services.Quests;

namespace PocketQuests.SchemaTests.Architecture;

/// <summary>Executable boundaries over the built product, with violating and conforming fixtures.</summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>Services may use typed Data interfaces/entities, but never EF or a context.</summary>
    [Fact]
    public void BusinessServices_DoNotDependOnEfOrDbContext() => Assert.Empty(ArchitectureRules.Violations(
        typeof(IQuestService).Assembly.GetTypes(), ArchitectureRules.IsPersistence));

    /// <summary>Wire contracts cannot expose or internally use persistence types, including wrapped entities.</summary>
    [Fact]
    public void PublicContracts_DoNotDependOnPersistence() => Assert.Empty(ArchitectureRules.Violations(
        typeof(QuestState).Assembly.GetTypes(), ArchitectureRules.IsData));

    /// <summary>Endpoint code delegates through Services rather than reaching Data, EF or Domain.</summary>
    [Fact]
    public void Endpoints_DoNotBypassServices()
    {
        var endpoints = typeof(PocketQuestsApiProgram).Assembly.GetTypes()
            .Where(type => ArchitectureRules.InNamespace(type, "PocketQuests.Api.Endpoints")).ToArray();
        Assert.NotEmpty(endpoints);
        Assert.Empty(ArchitectureRules.Violations(endpoints, ArchitectureRules.BypassesService));
    }

    /// <summary>Persistence owns its inputs and results instead of coupling queries to wire DTOs.</summary>
    [Fact]
    public void DataServices_DoNotDependOnPublicContracts() => Assert.Empty(ArchitectureRules.Violations(
        typeof(AppDbContext).Assembly.GetTypes(), ArchitectureRules.IsContract));

    /// <summary>Domain depends only on pure product, BCL and NodaTime types, without host I/O dependencies.</summary>
    [Fact]
    public void Domain_DoesNotDependOnOuterLayersOrHostIo() => Assert.Empty(ArchitectureRules.Violations(
        typeof(QuestAggregate).Assembly.GetTypes(), ArchitectureRules.IsImpureDomainDependency));

    /// <summary>Every production predicate rejects a deliberately invalid compiled fixture and accepts a valid one.</summary>
    [Fact]
    public void Rules_RejectViolationsAndAcceptSupportedBoundaries()
    {
        Verify(typeof(BadBusinessService), typeof(ValidBusinessService), ArchitectureRules.IsPersistence);
        Verify(typeof(BadContract), typeof(ValidContract), ArchitectureRules.IsData);
        Verify(typeof(BadEndpoint), typeof(ValidEndpoint), ArchitectureRules.BypassesService);
        Verify(typeof(BadDataService), typeof(ValidDataService), ArchitectureRules.IsContract);
        Verify(typeof(BadDomainRule), typeof(ValidDomainRule), ArchitectureRules.IsImpureDomainDependency);
    }

    /// <summary>Method-body dependencies and compiler-generated async code cannot evade injection checks.</summary>
    [Fact]
    public void Rules_InspectStaticBodiesAndAsyncStateMachines()
    {
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(BodyOnlyBusinessService)], ArchitectureRules.IsPersistence));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(AsyncEndpoint)], ArchitectureRules.BypassesService));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(GenericContextService)], ArchitectureRules.IsPersistence));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(AttributeContract)], ArchitectureRules.IsData));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(ConstrainedBusinessService<>)], ArchitectureRules.IsPersistence));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(CatchOnlyBusinessService)], ArchitectureRules.IsPersistence));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(ConcreteRepositoryService)], ArchitectureRules.IsPersistence));
        Assert.NotEmpty(ArchitectureRules.Violations([typeof(WrappedFactoryService)], ArchitectureRules.IsPersistence));
    }

    private static void Verify(Type invalid, Type valid, Func<Type, bool> forbidden)
    {
        var violations = ArchitectureRules.Violations([invalid], forbidden);
        Assert.NotEmpty(violations);
        Assert.All(violations, violation => Assert.StartsWith(invalid.FullName + " -> ", violation, StringComparison.Ordinal));
        Assert.Empty(ArchitectureRules.Violations([valid], forbidden));
    }
}
