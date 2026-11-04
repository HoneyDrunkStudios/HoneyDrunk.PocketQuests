using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PocketQuests.Api.Administration;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PocketQuests.SchemaTests;

/// <summary>Operator authorization, browser-session protection and SQL-backed product moderation.</summary>
public sealed partial class RelationalApiTests
{
    /// <summary>Only the explicitly configured canonical Identity account can read or change moderation state.</summary>
    /// <returns>Completion after anonymous, ordinary-user and empty-configuration denial.</returns>
    [Fact]
    public async Task AdminDirectoryIsDeniedByDefaultAndRestrictedToConfiguredAccount()
    {
        var admin = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: admin);
        using var anonymous = host.CreateClient();
        using var ordinary = host.Client();
        using var allowed = host.Client(admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/users/search", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/users/search", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/admin/users/access", new SetUserAccessRequest(host.Owner, true, "private", 0))).StatusCode);
        (await allowed.PostAsJsonAsync("/api/admin/users/search", new { query = "not-a-real-player" })).EnsureSuccessStatusCode();
        using var unconfigured = new Host(fixture.Connection);
        using var notAllowed = unconfigured.Client(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await notAllowed.PostAsJsonAsync("/api/admin/users/search", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await anonymous.GetAsync(new Uri("/admin/login", UriKind.Relative))).StatusCode);
        using var shell = await anonymous.GetAsync(new Uri("/admin/index.html", UriKind.Relative));
        shell.EnsureSuccessStatusCode();
        Assert.Contains("no-store", shell.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.True(shell.Headers.Contains("Content-Security-Policy"));
        var assets = AdminAssetLinks().Matches(await shell.Content.ReadAsStringAsync());
        Assert.True(assets.Count >= 2, "The built React script and stylesheet must be packaged with the API.");
        foreach (System.Text.RegularExpressions.Match asset in assets)
            (await anonymous.GetAsync(new Uri(asset.Groups[1].Value, UriKind.Relative))).EnsureSuccessStatusCode();
    }

    /// <summary>Moderation is versioned and atomic, blocks product access, preserves progress, and erases private evidence with its target.</summary>
    /// <returns>Completion after suspension, stale-edit rejection, reactivation and lifecycle erasure.</returns>
    [Fact]
    public async Task AdminModerationPreservesProgressRejectsStaleWritesAndErasesPrivateState()
    {
        var admin = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: admin);
        using var player = host.Client();
        using var operatorClient = host.Client(admin);
        await Setup(player);
        await Setup(operatorClient);
        var occurrence = Guid.NewGuid();
        await Command(player, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: "PQ-CAT-Q01"));
        var progress = await Command(player, new(Guid.NewGuid(), QuestActions.Complete, occurrence));
        var request = new SetUserAccessRequest(host.Owner, true, "  Review account access  ", 0);
        var suspended = await Access(operatorClient, request);
        Assert.True(suspended.Suspended);
        Assert.Equal("Review account access", suspended.Reason);
        Assert.Equal(1, suspended.Revision);
        Assert.Equal(HttpStatusCode.Unauthorized, (await player.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await player.PostAsJsonAsync("/api/commands", new { })).StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Commands().Read(new AccountIdentity("verified-identity", host.Owner), Start));
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsJsonAsync("/api/admin/users/access", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await operatorClient.PostAsJsonAsync("/api/admin/users/access", request with { UserId = admin })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await operatorClient.PostAsJsonAsync("/api/admin/users/access", request with { Reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await operatorClient.PostAsJsonAsync("/api/admin/users/access", request with { UserId = NewOwner() })).StatusCode);
        var reactivated = await Access(operatorClient, request with { Suspended = false, ExpectedRevision = 1, Reason = "Review complete" });
        Assert.False(reactivated.Suspended);
        Assert.Equal(2, reactivated.Revision);
        var after = await Read(player);
        Assert.Equal(progress.OverallXp, after.OverallXp);
        Assert.Equal(JsonSerializer.Serialize(progress.Occurrences, Json), JsonSerializer.Serialize(after.Occurrences, Json));
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(row => row.IdentityUserId == host.Owner);
        var audits = await fixture.Query($"SELECT EventName,MetadataJson FROM dbo.AuditRecords a JOIN pocketquests.AccountAuditRecord own ON own.AuditRecordId=a.Id WHERE own.AccountId='{account.Id:D}' AND a.EventName LIKE 'pocketquests.admin.%';");
        Assert.Equal(2, audits[0].Rows.Count);
        Assert.DoesNotContain("Review account access", JsonSerializer.Serialize(audits[0].Rows.Cast<System.Data.DataRow>().Select(row => row.ItemArray)), StringComparison.Ordinal);
        var intent = new LifecycleIntent(host.Owner, 2, IdentityProtocol.Erasing, Start.AddDays(1), Start.AddDays(1).AddHours(1), IdentityProtocol.ConsumerId, new string('a', 64), Start);
        await fixture.Lifecycle().ReceiveLifecycle(intent, "private-ack", Start.AddDays(1));
        Assert.False(await db.Set<AccountAccessRestrictionEntity>().AnyAsync(row => row.AccountId == account.Id));
        Assert.False(await db.Set<AccountAuditRecordEntity>().AnyAsync(row => row.AccountId == account.Id));
        var directory = await operatorClient.PostAsJsonAsync("/api/admin/users/search", new { query = host.Owner });
        Assert.Empty((await directory.Content.ReadFromJsonAsync<AdminUserPage>(Json))!.Users);
    }

    /// <summary>A cookie session requires anti-forgery proof, even when a forged bearer header is also supplied.</summary>
    /// <returns>Completion after real encrypted-cookie validation and CSRF checks.</returns>
    [Fact]
    public async Task AdminCookieRequiresCsrfAndRejectsInactiveIdentity()
    {
        var admin = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: admin);
        using var registered = host.Client(admin);
        using var cookieClient = host.CreateClient();
        var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AdminWeb.Cookie);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "honeydrunk-identity"), new Claim("sub", admin)], AdminWeb.Cookie));
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = admin }]);
        var cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(principal, properties, AdminWeb.Cookie));
        cookieClient.DefaultRequestHeaders.Add("Cookie", "PocketQuests.Admin=" + cookie);
        Assert.Equal(HttpStatusCode.BadRequest, (await cookieClient.PostAsJsonAsync("/api/admin/users/search", new { })).StatusCode);
        cookieClient.DefaultRequestHeaders.Authorization = new("Bearer", "forged-token");
        Assert.Equal(HttpStatusCode.BadRequest, (await cookieClient.PostAsJsonAsync("/api/admin/users/search", new { })).StatusCode);
        cookieClient.DefaultRequestHeaders.Authorization = null;
        using var session = await cookieClient.GetAsync(new Uri("/api/admin/session", UriKind.Relative));
        session.EnsureSuccessStatusCode();
        Assert.Contains("no-store", session.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var body = await session.Content.ReadFromJsonAsync<JsonElement>();
        cookieClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("csrfToken").GetString());
        (await cookieClient.PostAsJsonAsync("/api/admin/users/search", new { query = "test" })).EnsureSuccessStatusCode();
        host.Users.Remove(admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cookieClient.GetAsync(new Uri("/api/admin/session", UriKind.Relative))).StatusCode);
    }

    /// <summary>Directory queries are bounded and cursor-based, with names safely treated as literal search terms.</summary>
    /// <returns>Completion after disjoint pagination and a literal-name lookup.</returns>
    [Fact]
    public async Task AdminDirectoryUsesBoundedPagesAndLiteralSearch()
    {
        var admin = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: admin);
        using var client = host.Client(admin);
        var label = "AdminPage" + Guid.NewGuid().ToString("N")[..12];
        var users = Enumerable.Range(0, 53).Select(_ => NewOwner()).ToArray();
        foreach (var user in users)
            await fixture.Commands().Initialize(new AccountIdentity("verified-identity", user), "Etc/UTC", Start);
        await fixture.Execute($"UPDATE pocketquests.Account SET CharacterName='{label}',PortraitId='portrait-1' WHERE IdentityUserId IN ({string.Join(',', users.Select(id => "'" + id + "'"))});");
        var first = await Page(client, label.ToLowerInvariant());
        Assert.Equal(50, first.Users.Length);
        Assert.NotNull(first.NextCursor);
        var second = await Page(client, label, first.NextCursor);
        Assert.Equal(3, second.Users.Length);
        Assert.Null(second.NextCursor);
        Assert.Equal(53, first.Users.Concat(second.Users).Select(user => user.UserId).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty((await Page(client, label + "%")).Users);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users/search", new { query = new string('x', 121) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users/search", new { after = "invalid" })).StatusCode);
    }

    /// <summary>Concurrent operators cannot overwrite an unseen change; SQL rejects invalid current-state writes.</summary>
    /// <returns>Completion after one committed race winner and deployed constraint checks.</returns>
    [Fact]
    public async Task AdminAccessSerializesConcurrentChangesAndEnforcesSqlBounds()
    {
        var admin = NewOwner();
        using var host = new Host(fixture.Connection, adminUserId: admin);
        using var player = host.Client();
        using var client = host.Client(admin);
        await Setup(player);
        var change = new SetUserAccessRequest(host.Owner, true, "Initial review", 0);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/admin/users/access", change), client.PostAsJsonAsync("/api/admin/users/access", change with { Reason = "Concurrent review" }));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses)
            response.Dispose();
        await using var db = fixture.Context();
        var account = await db.Set<AccountEntity>().SingleAsync(row => row.IdentityUserId == host.Owner);
        var restriction = await db.Set<AccountAccessRestrictionEntity>().SingleAsync(row => row.AccountId == account.Id);
        Assert.Equal(1, restriction.Revision);
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.AccountAccessRestriction SET Revision=0 WHERE Id='{restriction.Id:D}';"));
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.AccountAccessRestriction SET Reason=N'  ' WHERE Id='{restriction.Id:D}';"));
        await Assert.ThrowsAsync<SqlException>(() => fixture.Execute($"UPDATE pocketquests.AccountAccessRestriction SET AccountId='{Guid.NewGuid():D}' WHERE Id='{restriction.Id:D}';"));
        Assert.Equal(1, (await Page(client, host.Owner)).Users.Single().Revision);
    }

    private static async Task<AdminUser> Access(HttpClient client, SetUserAccessRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/users/access", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminUser>(Json))!;
    }

    private static async Task<AdminUserPage> Page(HttpClient client, string query, string? after = null)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/users/search", new { query, after });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminUserPage>(Json))!;
    }

    [GeneratedRegex("(?:src|href)=\"(/admin/assets/[^\"]+)\"")]
    private static partial Regex AdminAssetLinks();
}
