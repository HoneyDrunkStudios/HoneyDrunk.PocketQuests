using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;

namespace PocketQuests.Services.Administration;

/// <summary>Operator-only directory and explicit audited access changes.</summary>
public interface IAdminUserService
{
    /// <summary>Reads one bounded page of product profile metadata.</summary>
    /// <param name="query">Optional literal search.</param>
    /// <param name="after">Optional exclusive canonical-user cursor.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>One page and its optional continuation cursor.</returns>
    Task<AdminUserPage> Search(string? query, string? after, CancellationToken token);

    /// <summary>Suspends or reactivates product access under the account command lock.</summary>
    /// <param name="request">Explicit access state, reason and expected revision.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The current committed product-directory status.</returns>
    Task<AdminUser> SetAccess(SetUserAccessRequest request, CancellationToken token);
}
