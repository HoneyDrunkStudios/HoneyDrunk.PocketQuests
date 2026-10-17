using PocketQuests.Services.Accounts;

namespace PocketQuests.ReplayProbe;

internal sealed class ProbeAccount : ICurrentAccount
{
    public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity => throw new InvalidOperationException("The probe supplies its verified synthetic identity explicitly.");
}
