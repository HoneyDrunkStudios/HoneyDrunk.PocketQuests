using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Quests;
using DomainIdentity = PocketQuests.Domain.Models.Accounts.AccountIdentity;
using LegacyService = PocketQuests.Domain.Services.Quests.IQuestService;

namespace PocketQuests.SchemaTests.Quests;

internal static class CompletionFixture
{
    internal static async Task<QuestState> Complete(this SchemaFixture fixture, DomainIdentity owner, QuestCommand command, DateTimeOffset at)
    {
        await using var scope = fixture.CreateScope();
        var services = scope.ServiceProvider;
        var service = new QuestService(services.GetRequiredService<IQuestCompletionDataService>(), new CurrentAccount(owner), new Clock(at), services.GetRequiredService<LegacyService>());
        return await service.Execute(command);
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
