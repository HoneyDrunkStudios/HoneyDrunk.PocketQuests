using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Services;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Profiles;
using PocketQuests.Services.Progress;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Schedules;
using DomainIdentity = PocketQuests.Domain.Models.Accounts.AccountIdentity;

namespace PocketQuests.SchemaTests.Quests;

internal static class CompletionFixture
{
    internal static async Task<QuestState> Complete(this SchemaFixture fixture, DomainIdentity owner, QuestCommand command, DateTimeOffset at)
    {
        await using var scope = fixture.CreateScope();
        var services = scope.ServiceProvider;
        var service = Create(services, owner, at);
        return await service.Execute(command);
    }

    internal static QuestService Create(IServiceProvider services, DomainIdentity owner, DateTimeOffset at) => new(
        services.GetRequiredService<IAccountDataService>(),
        new CurrentAccount(owner),
        new Clock(at),
        services.GetRequiredService<ProfileHistoryService>(),
        services.GetRequiredService<QuestDefinitionService>(),
        services.GetRequiredService<QuestSeriesService>(),
        services.GetRequiredService<QuestOccurrenceService>(),
        services.GetRequiredService<ProgressService>(),
        services.GetRequiredService<QuestCommandHistoryService>());

    // A single externally owned context lets fault-injection interceptors exercise every typed repository.
    internal static ServiceProvider Provider(AppDbContext db, string connection)
    {
        var services = new ServiceCollection();
        services.AddQuestDataServices(connection);
        services.AddSingleton(db);
        services.AddQuestServices();
        return services.BuildServiceProvider();
    }

    private sealed class CurrentAccount(DomainIdentity identity) : ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity { get; } = new(identity.Issuer, identity.Subject);
    }

    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }
}
