using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.Tests.Fixtures;

/// <summary>Loopback-only external-provider test double; product and Identity authentication remain real.</summary>
internal static class NativeTestProvider
{
    internal static async Task<WebApplication> Start(string token)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://localhost:5219");
        var app = builder.Build();
        var codes = new ConcurrentDictionary<string, (string challenge, string redirect, DateTimeOffset expires)>();
        app.MapGet("/.well-known/openid-configuration", () => Results.Ok(new
        {
            issuer = "https://identity.test",
            authorization_endpoint = "http://localhost:5219/authorize",
            token_endpoint = "http://localhost:5219/token",
            response_types_supported = new[] { "code" },
            code_challenge_methods_supported = new[] { "S256" },
        }));
        app.MapGet("/authorize", (HttpContext context) =>
        {
            var query = context.Request.Query;
            var redirect = query["redirect_uri"].ToString();
            if (query["client_id"] != "native-fixture" || query["response_type"] != "code"
                || query["code_challenge_method"] != "S256" || query["code_challenge"].ToString().Length != 43
                || redirect != "pocketquests://callback" || string.IsNullOrEmpty(query["state"]))
                return Results.BadRequest();
            var code = Guid.NewGuid().ToString("N");
            codes[code] = (query["code_challenge"].ToString(), redirect, DateTimeOffset.UtcNow.AddMinutes(1));
            return Results.Redirect(redirect + "?code=" + code + "&state=" + Uri.EscapeDataString(query["state"].ToString()));
        });
        app.MapPost("/token", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            if (!codes.TryRemove(form["code"].ToString(), out var expected) || expected.expires <= DateTimeOffset.UtcNow
                || form["client_id"] != "native-fixture" || form["redirect_uri"] != expected.redirect || form["grant_type"] != "authorization_code")
                return Results.BadRequest();
            var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return challenge != expected.challenge ? Results.BadRequest() : Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 600 });
        });
        await app.StartAsync();
        return app;
    }
}
