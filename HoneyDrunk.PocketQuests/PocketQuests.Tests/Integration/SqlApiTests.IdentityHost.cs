using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Identity.AccountLifecycle;
using HoneyDrunk.Identity.Api;
using HoneyDrunk.Identity.Client;
using HoneyDrunk.Identity.Persistence.Context;
using HoneyDrunk.Identity.Providers.Entra.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PocketQuests.Api;
using PocketQuests.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace PocketQuests.Tests.Integration;

/// <summary>Real Identity source host for cross-service authentication and lifecycle acceptance.</summary>
public sealed partial class SqlApiTests : IAsyncLifetime
{
    private readonly string database = "PocketQuests_Tests_" + Guid.NewGuid().ToString("N");

    private string Connection => $"Server=(localdb)\\PocketQuests;Database={database};Integrated Security=true;Encrypt=true;TrustServerCertificate=true";

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await using var db = Context();
        await DatabaseSchema.DeployAsync(db.Database);
        await using var identity = IdentityContext();
        await DatabaseSchema.DeployAsync(identity.Database, "HoneyDrunk.Identity.Database");
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await DisposePersistence();
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await using var identity = IdentityContext();
        await identity.Database.EnsureDeletedAsync();
    }

    /// <summary>Verifies wrong signature and audience are rejected.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task WrongSignatureAndAudienceAreRejected()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice", audience: "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid.token.value");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        using var forged = host.Client("alice", wrongSignature: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
    }

    /// <summary>Verifies public Grid headers cannot assign audit ownership or correlation.</summary>
    /// <returns>A task completing after the persisted audit checks.</returns>
    [Fact]
    public async Task PublicGridHeadersCannotReattributeAudit()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "forged-correlation");
        await Setup(client);
        await using var identity = IdentityContext();
        var audits = await identity.Audit.ToListAsync();
        Assert.NotEmpty(audits);
        Assert.All(audits, audit =>
        {
            Assert.Equal(HoneyDrunk.Kernel.Abstractions.Identity.TenantId.Internal.ToString(), audit.TenantId);
            Assert.NotEqual("forged-correlation", audit.CorrelationId);
            Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
        });
    }

    private async Task Republish()
    {
        await using var db = Context();
        await DatabaseSchema.DeployAsync(db.Database);
    }

    private IdentityDbContext IdentityContext() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(Connection.Replace(database, database + "_Identity")).Options);

    private sealed class Host(string connection, bool networkIdentity = false, TimeProvider? clock = null) : WebApplicationFactory<PocketQuestsApiProgram>
    {
        private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("test-only-signing-key-for-actual-jwt-validation-0123456789"));
        private readonly IdentityHost identityHost = new(connection.Replace(";Integrated Security", "_Identity;Integrated Security"), networkIdentity);

        public HoneyDrunk.Identity.Abstractions.Authentication.IExternalAccounts External => identityHost.Services.GetRequiredService<HoneyDrunk.Identity.Abstractions.Authentication.IExternalAccounts>();

        public HttpClient Client(string subject, string audience = "quests", bool wrongSignature = false)
        {
            var signingKey = wrongSignature ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("untrusted-test-signing-key-that-must-never-be-accepted-0123456789")) : Key;
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = "https://identity.test", Audience = audience, Subject = new ClaimsIdentity([new("sub", subject), new("scp", "access_as_user"), new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)]), Expires = DateTime.UtcNow.AddMinutes(10), SigningCredentials = new(signingKey, SecurityAlgorithms.HmacSha256) });
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:quests", connection);
            builder.UseSetting("Persistence:ReconciliationEnabled", "false");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:quests"] = connection }));
            if (clock is not null)
                builder.ConfigureServices(services => services.AddSingleton(clock));
            if (networkIdentity)
            {
                identityHost.UseKestrel(5218);
                _ = identityHost.CreateClient();
                builder.ConfigureServices(services => services.AddHttpClient<IdentityClient>(client => client.BaseAddress = new Uri("http://localhost:5218")));
            }
            else
            {
                builder.ConfigureServices(services => services.AddHttpClient<IdentityClient>().ConfigurePrimaryHttpMessageHandler(() => identityHost.Server.CreateHandler()));
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                identityHost.Dispose();
        }

        private sealed class IdentityHost(string identityConnection, bool nativeFixture) : WebApplicationFactory<IdentityApiProgram>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:identity"] = identityConnection,
                    ["Entra:Authority"] = nativeFixture ? "http://localhost:5219" : null,
                    ["Entra:MobileClientId"] = nativeFixture ? "native-fixture" : null,
                    ["Entra:ApiScope"] = nativeFixture ? "quests" : null,
                    ["Entra:Issuer"] = nativeFixture ? "https://identity.test" : null,
                    ["Entra:Audience"] = nativeFixture ? "quests" : null,
                }));
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(new EntraSigningKeys(new ConfigurationBuilder().Build()));
                    services.AddSingleton<ISigningKeyProvider>(new TestKeys());
                    services.AddSingleton<HoneyDrunk.Identity.Abstractions.Authentication.IExternalAccounts>(new TestExternalAccounts());
                    services.PostConfigure<LifecycleOptions>(options =>
                    {
                        options.DeliveryEnabled = true;
                        options.Consumers["pocketquests"] = "test-only-private-queue";
                    });
                });
            }
        }

        private sealed class TestKeys : ISigningKeyProvider
        {
            public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SecurityKey>>([Key]);

            public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default) => Task.FromResult("https://identity.test");

            public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default) => Task.FromResult("quests");

            public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default) => Task.FromResult(TimeSpan.Zero);
        }
    }
}
