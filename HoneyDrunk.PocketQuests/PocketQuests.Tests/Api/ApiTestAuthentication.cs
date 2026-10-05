using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PocketQuests.Tests.Api;

internal sealed class ApiTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Headers.Authorization.ToString();
        if (token == "Bearer synthetic-auth-failure")
            throw new InvalidOperationException("synthetic-private-token");
        if (token is not ("Bearer synthetic-a" or "Bearer synthetic-b"))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new ClaimsIdentity([new Claim("iss", "synthetic-issuer"), new Claim("sub", token)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(claims), Scheme.Name)));
    }
}
