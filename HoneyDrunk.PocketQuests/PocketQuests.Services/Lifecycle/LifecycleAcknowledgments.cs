using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Services.Lifecycle.Mapping;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.Services.Lifecycle;

internal static class LifecycleAcknowledgments
{
    internal static async Task Stage(IAccountDataService accounts, ILifecycleMessageDataService messages, LifecycleIntent intent, string queue, DateTimeOffset now, CancellationToken token)
    {
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{intent.UserId}/{intent.Version}/{intent.Acknowledgment}")).AsSpan(0, 16));
        var prior = await messages.FindByIdAsync(id, token);
        if (prior is not null)
        {
            if (prior.IdentityUserId != intent.UserId || prior.LifecycleVersion != intent.Version)
                throw new InvalidOperationException("Acknowledgment identifier belongs to another owner or version.");
            return;
        }

        var account = await accounts.GetByIdentityUserId(intent.UserId, token);
        var expiresAt = (intent.ExpiresAt < now.AddHours(1) ? intent.ExpiresAt : now.AddHours(1)).ToUniversalTime();
        await messages.AddWithEnvelope(intent.ToOwnership(id, account?.Id, expiresAt, now), intent.ToEnvelope(id, queue, now), token);
    }
}
