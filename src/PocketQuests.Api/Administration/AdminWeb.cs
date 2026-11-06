using HoneyDrunk.Identity.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Administration;
using System.Security.Claims;

namespace PocketQuests.Api.Administration;

/// <summary>Same-origin operator web sign-in; Identity remains the canonical account resolver.</summary>
public static class AdminWeb
{
    /// <summary>The single-operator authorization policy.</summary>
    public const string Policy = "PocketQuestsAdmin";

    /// <summary>The encrypted HttpOnly administrative session.</summary>
    public const string Cookie = "AdminCookie";

    /// <summary>The standard authorization-code/PKCE sign-in handler.</summary>
    public const string Oidc = "AdminOidc";

    /// <summary>Registers cookie protections and optional public-client OIDC configuration.</summary>
    /// <param name="builder">The API host and explicit operator settings.</param>
    public static void AddAdminWeb(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<AdminAccessOptions>(builder.Configuration.GetSection("Admin"));
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes("Identity", Cookie).RequireAuthenticatedUser()
            .RequireAssertion(context => IsConfiguredOperator(context.Resource as HttpContext, context.User)));
        var authentication = builder.Services.AddAuthentication();
        authentication.AddCookie(Cookie, options =>
        {
            options.Cookie.Name = "PocketQuests.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = ValidateSession;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });
        var authority = builder.Configuration["Admin:Authority"];
        var clientId = builder.Configuration["Admin:ClientId"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
            return;
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Admin:Authority must be the HTTPS provider authority.");
        authentication.AddOpenIdConnect(Oidc, options =>
        {
            options.SignInScheme = Cookie;
            options.Authority = authority;
            options.ClientId = clientId;
            options.ResponseType = "code";
            options.UsePkce = true;
            options.SaveTokens = true;
            options.MapInboundClaims = false;
            options.CallbackPath = "/admin/signin-oidc";
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            foreach (var scope in (builder.Configuration["Admin:Scope"] ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                options.Scope.Add(scope);
            options.Events.OnTokenValidated = ValidateOperatorSignIn;
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/admin/index.html?signin=failed");
                return Task.CompletedTask;
            };
        });
    }

    /// <summary>Protects administrative pages and API responses from embedding, caching and inline script execution.</summary>
    /// <param name="app">The configured web host.</param>
    public static void UseAdminWeb(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin") || context.Request.Path.StartsWithSegments("/api/admin"))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
            }

            await next(context);
        });
        app.MapStaticAssets();
    }

    private static async Task ValidateOperatorSignIn(TokenValidatedContext context)
    {
        var token = context.TokenEndpointResponse?.AccessToken;
        var identity = context.HttpContext.RequestServices.GetRequiredService<IdentityClient>();
        var user = string.IsNullOrEmpty(token) ? null : await identity.Resolve(token, context.HttpContext.RequestAborted);
        var configured = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminAccessOptions>>().Value.UserId;
        if (user is null || user.State != IdentityProtocol.Active || string.IsNullOrEmpty(configured) || user.UserId != configured)
        {
            context.Fail("Administrative sign-in is not permitted.");
            return;
        }

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "honeydrunk-identity"), new Claim("sub", user.UserId)], Cookie));
    }

    private static bool IsConfiguredOperator(HttpContext? http, ClaimsPrincipal user)
    {
        var configured = http?.RequestServices.GetRequiredService<IOptions<AdminAccessOptions>>().Value.UserId;
        return !string.IsNullOrEmpty(configured) && user.FindFirstValue("iss") == "honeydrunk-identity"
            && user.FindFirstValue("sub") == configured;
    }

    private static async Task ValidateSession(CookieValidatePrincipalContext context)
    {
        var token = context.Properties.GetTokenValue("access_token");
        var configured = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminAccessOptions>>().Value.UserId;
        try
        {
            var identity = context.HttpContext.RequestServices.GetRequiredService<IdentityClient>();
            var user = string.IsNullOrEmpty(token) ? null : await identity.Resolve(token, context.HttpContext.RequestAborted);
            if (user is null || user.State != IdentityProtocol.Active || string.IsNullOrEmpty(configured)
                || user.UserId != configured || context.Principal?.FindFirstValue("sub") != user.UserId)
                context.RejectPrincipal();
        }
        catch (HttpRequestException)
        {
            context.RejectPrincipal();
        }
    }
}
