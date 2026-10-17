namespace PocketQuests.Services.Quests.Mapping;

internal static class AccountIdentityMapping
{
    internal static PocketQuests.Domain.Models.Accounts.AccountIdentity ToModel(this PocketQuests.Contracts.Models.Accounts.AccountIdentity identity) => new(identity.Issuer, identity.Subject);
}
