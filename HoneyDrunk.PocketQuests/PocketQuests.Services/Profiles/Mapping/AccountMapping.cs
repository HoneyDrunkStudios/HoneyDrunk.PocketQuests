using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;

namespace PocketQuests.Services.Profiles.Mapping;

internal static class AccountMapping
{
    internal static AccountEntity Create(Guid id, AccountIdentity identity, string zone, DateTimeOffset now) => new()
    {
        Id = id,
        IdentityUserId = identity.Subject,
        TimeZoneId = zone,
        LastRecordedAt = now,
        ProjectionAsOfAt = now,
    };
}
