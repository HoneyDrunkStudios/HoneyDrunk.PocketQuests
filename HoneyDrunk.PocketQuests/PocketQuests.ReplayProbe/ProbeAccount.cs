using PocketQuests.Services.Accounts;

internal sealed class ProbeAccount : ICurrentAccount
{
    public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity => throw new InvalidOperationException("The probe supplies its verified synthetic identity explicitly.");
}
