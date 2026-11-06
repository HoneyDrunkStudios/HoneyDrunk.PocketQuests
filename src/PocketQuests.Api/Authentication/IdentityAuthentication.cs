using HoneyDrunk.Identity.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Administration;
using PocketQuests.Services.Lifecycle;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PocketQuests.Api.Authentication;

/// <summary>Resolves bearer credentials exclusively through the shared Identity boundary.</summary>
/// <param name="options">The authentication scheme options.</param>
/// <param name="logger">The logger for this component.</param>
/// <param name="encoder">The URL encoder used by the authentication handler.</param>
/// <param name="identity">The Identity client used to validate account access.</param>
/// <param name="lifecycle">The scoped account lifecycle service.</param>
/// <param name="access">The account access restriction service.</param>
public sealed class IdentityAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IdentityClient identity, ILifecycleService lifecycle, IAccountAccessService access)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();
        try
        {
            var user = await identity.Resolve(header[7..].Trim(), Context.RequestAborted);
            if (user is null || user.State != IdentityProtocol.Active)
                return AuthenticateResult.Fail("Sign in again.");
            if (await access.IsSuspended(user.UserId, Context.RequestAborted))
                return AuthenticateResult.Fail("Pocket Quests access is suspended.");
            await lifecycle.ObserveActive(user, Context.RequestAborted);
            var claims = new ClaimsIdentity([new Claim("iss", "honeydrunk-identity"), new Claim("sub", user.UserId)], Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(claims), Scheme.Name));
        }
        catch (UnauthorizedAccessException)
        {
            return AuthenticateResult.Fail("Account lifecycle changed.");
        }
        catch (HttpRequestException)
        {
            // An unavailable Identity service never falls back to a local/guest owner.
            return AuthenticateResult.Fail("Identity service is unavailable. Retry shortly.");
        }
    }
}
