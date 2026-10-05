using PocketQuests.Contracts.Requests.Profiles;
using PocketQuests.Contracts.Responses.Projections;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Schedules;
using PocketQuests.Services.Accounts;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Profiles;

/// <summary>Owns explicit account creation; reads never create profiles.</summary>
/// <param name="data">Scoped account persistence.</param>
/// <param name="quests">Shared operation access checks and source reads.</param>
/// <param name="currentAccount">Authenticated identity.</param>
/// <param name="clock">Host time.</param>
public sealed class ProfileService(IAccountDataService data, QuestService quests, ICurrentAccount currentAccount, TimeProvider clock) : IProfileService
{
    /// <inheritdoc />
    public async Task<QuestState> Initialize(InitializeProfile request, CancellationToken token = default)
    {
        var identity = currentAccount.Identity.ToModel();
        var now = clock.GetUtcNow();
        await Initialize(identity, request.Zone, now, token);
        return (await quests.Read(identity, now, token)).ToModel();
    }

    internal Task<bool> Initialize(AccountIdentity identity, string zone, DateTimeOffset now, CancellationToken token = default)
    {
        var canonicalZone = Scheduling.Zone(zone).Id;
        return data.ExecuteInTransaction(Perform, token);

        async Task<bool> Perform(CancellationToken cancellationToken)
        {
            await quests.RequireAccess(identity, true, cancellationToken);
            if (await data.GetByIdentityUserId(identity.Subject, cancellationToken) is not null)
                return false;
            var account = AccountMapping.Create(identity, canonicalZone, now.ToUniversalTime());
            await data.AddAsync(account, cancellationToken);
            var barrier = await data.GetLifecycle(identity.Subject, cancellationToken);
            if (barrier is not null)
                barrier.AccountId = account.Id;
            return true;
        }
    }
}
