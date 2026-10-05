using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;

namespace PocketQuests.Services.Profiles.Mapping;

internal static class AccountMapping
{
    internal static AccountEntity Create(AccountIdentity identity, string zone, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        IdentityUserId = identity.Subject,
        TimeZoneId = zone,
        LastRecordedAt = now,
        ProjectionAsOfAt = now,
        CreatedAt = now,
        ModifiedAt = now,
    };
}
