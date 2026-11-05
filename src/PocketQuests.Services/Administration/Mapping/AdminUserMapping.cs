using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Data.Queries.Administration;

namespace PocketQuests.Services.Administration.Mapping;

internal static class AdminUserMapping
{
    internal static AdminUser ToContract(this AdminUserRow row) => new(row.UserId, row.CharacterName, row.Zone, row.OnboardingComplete, row.CreatedAt, row.Suspended, row.Reason, row.Revision);
}
