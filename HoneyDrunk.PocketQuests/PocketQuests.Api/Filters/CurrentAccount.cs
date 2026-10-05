using PocketQuests.Contracts.Models.Accounts;
using PocketQuests.Services.Accounts;
using System.Security.Claims;

namespace PocketQuests.Api.Filters;

/// <summary>Exposes only the claims established by the host authentication handler.</summary>
/// <param name="context">Current authenticated request.</param>
internal sealed class CurrentAccount(IHttpContextAccessor context) : ICurrentAccount
{
    public AccountIdentity Identity
    {
        get
        {
            var principal = context.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException();
            return new(principal.FindFirstValue("iss") ?? throw new UnauthorizedAccessException(), principal.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());
        }
    }
}
