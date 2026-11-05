using PocketQuests.Data.DataServices.Accounts;

namespace PocketQuests.Services.Administration;

/// <summary>Reads current access through the account persistence boundary.</summary>
/// <param name="accounts">Scoped account persistence.</param>
public sealed class AccountAccessService(IAccountDataService accounts) : IAccountAccessService
{
    /// <inheritdoc />
    public Task<bool> IsSuspended(string userId, CancellationToken token) => accounts.IsAccessSuspended(userId, token);
}
