using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PocketQuests.Api.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Api;

/// <summary>Tests local-process limits, browser origin rules and trusted TLS forwarding.</summary>
public sealed class ApiHttpPolicyTests
{
    /// <summary>Each verified account receives separate command and export budgets.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task LimitsReturn429WithRetryAfterAndAccountIsolation()
    {
        await using var host = await ApiTestHost.Start(configuration: new()
        {
            ["Api:Http:CommandPermitLimit"] = "1",
            ["Api:Http:ExportPermitLimit"] = "1",
        });
        var command = new { operationId = Guid.NewGuid(), action = "test" };
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/commands", command)).StatusCode);
        var limited = await host.Client.PostAsJsonAsync("/api/commands", command);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(1, host.Store.Commands);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(new Uri("/api/export/json", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.GetAsync(new Uri("/api/export/csv", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-b");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/commands", command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(new Uri("/api/export/json", UriKind.Relative))).StatusCode);
    }

    /// <summary>CORS exposes only configured browser origins and does not authenticate requests.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task CorsAllowsOnlyConfiguredOriginsAndStillRequiresAuthentication()
    {
        await using var host = await ApiTestHost.Start(configuration: new() { ["Api:Http:AllowedOrigins:0"] = "https://browser.example.test" });
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/commands");
        preflight.Headers.Add("Origin", "https://browser.example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var response = await host.Client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://browser.example.test", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        host.Client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example.test");
        Assert.False((await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative))).Headers.Contains("Access-Control-Allow-Origin"));
        host.Client.DefaultRequestHeaders.Remove("Origin");
        host.Client.DefaultRequestHeaders.Add("Origin", "https://browser.example.test");
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
    }

    /// <summary>Production redirects HTTP and accepts TLS assertions only from a configured immediate proxy.</summary>
    /// <param name="trust">Whether an immediate proxy is configured.</param>
    /// <param name="remote">The connecting peer.</param>
    /// <param name="status">The expected result.</param>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Theory]
    [InlineData(false, "192.0.2.10", 307)]
    [InlineData(true, "192.0.2.99", 307)]
    [InlineData(true, "192.0.2.10", 200)]
    public async Task ProductionDoesNotTrustArbitraryForwardedHeaders(bool trust, string remote, int status)
    {
        var configuration = new Dictionary<string, string?>();
        if (trust)
            configuration["Api:Http:KnownProxies:0"] = "192.0.2.10";
        await using var host = await ApiTestHost.Start("Production", configuration, IPAddress.Parse(remote));
        host.Client.BaseAddress = new Uri("http://api.example.test");
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.20");
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-Host", "untrusted.example.test");
        var response = await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 307)
            Assert.Equal("https://api.example.test/api/state", response.Headers.Location?.ToString());
        else
            Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    /// <summary>Local HTTP remains usable while production has no implicit browser origin permission.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task DefaultsAreEnvironmentSensitive()
    {
        await using var local = await ApiTestHost.Start("Development");
        local.Client.BaseAddress = new Uri("http://api.example.test");
        local.Client.DefaultRequestHeaders.Add("Origin", "http://localhost:8081");
        var allowed = await local.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True(allowed.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(allowed.Headers.Contains("Strict-Transport-Security"));
        await using var production = await ApiTestHost.Start("Production");
        production.Client.DefaultRequestHeaders.Add("Origin", "http://localhost:8081");
        var denied = await production.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(denied.Headers.Contains("Strict-Transport-Security"));
    }

    /// <summary>Invalid policy configuration fails at startup instead of silently removing protection.</summary>
    /// <param name="key">The invalid setting.</param>
    /// <param name="value">The rejected value.</param>
    [Theory]
    [InlineData("CommandPermitLimit", "0")]
    [InlineData("ExportPermitLimit", "-1")]
    [InlineData("KnownProxies:0", "*")]
    [InlineData("AllowedOrigins:0", "*")]
    [InlineData("AllowedOrigins:0", "https://browser.example.test/path")]
    [InlineData("AllowedOrigins:0", "http://browser.example.test")]
    public void InvalidProductionConfigurationFailsClosed(string key, string value)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Api:Http:" + key] = value });
        Assert.Throws<OptionsValidationException>(() => builder.AddApiHttpPolicy());
    }
}
