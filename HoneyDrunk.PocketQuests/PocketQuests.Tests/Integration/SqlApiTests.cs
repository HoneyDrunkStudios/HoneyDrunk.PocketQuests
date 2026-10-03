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
using PocketQuests.Data.Context;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using PocketQuests.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Tests.Integration;

/// <summary>Executable regression coverage for sql api tests.</summary>
public sealed partial class SqlApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
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
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
        await using var identity = IdentityContext();
        await identity.Database.EnsureDeletedAsync();
    }

    /// <summary>Verifies sql state survives api restart and undo is durable.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task SqlStateSurvivesApiRestartAndUndoIsDurable()
    {
        Guid occurrence;
        Guid completion;
        var completeOperation = Guid.NewGuid();
        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
        {
            await Setup(client);
            var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01"));
            occurrence = accepted.Occurrences.Single().Occurrence.Id;
            var complete = await Command(client, new(completeOperation, "complete", occurrence));
            Assert.Equal(10, complete.OverallXp);
            completion = complete.Occurrences.Single().Completion!.Id;
        }

        // Entire web host, DI scopes and DbContexts are recreated. SQL remains.
        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
        {
            Assert.Equal(10, (await Read(client)).OverallXp);
            var replay = await Command(client, new(completeOperation, "complete", occurrence));
            Assert.Equal(completion, replay.Occurrences.Single().Completion!.Id);
            var undo = await Command(client, new(Guid.NewGuid(), "undo", occurrence, CompletionId: completion));
            Assert.Equal(0, undo.OverallXp);
        }

        using (var host = new Host(Connection))
        using (var client = host.Client("alice"))
            Assert.Equal(0, (await Read(client)).OverallXp);
        await using var db = Context();
        Assert.Equal(1, await db.Completions.CountAsync());
        Assert.Equal(1, await db.Undos.CountAsync());
        Assert.Equal(3, await db.Audit.CountAsync());
    }

    /// <summary>Verifies concurrent hosts cannot double grant or accept same operation twice.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task ConcurrentHostsCannotDoubleGrantOrAcceptSameOperationTwice()
    {
        using var a = new Host(Connection);
        using var b = new Host(Connection);
        using var first = a.Client("alice");
        using var second = b.Client("alice");
        await Setup(first);
        var accept = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q07");
        var results = await Task.WhenAll(Command(first, accept), Command(second, accept));
        var id = results[0].Occurrences.Single().Occurrence.Id;
        Assert.Equal(id, results[1].Occurrences.Single().Occurrence.Id);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Command(i % 2 == 0 ? first : second, new(Guid.NewGuid(), "complete", id))));
        Assert.Equal(10, (await Read(first)).OverallXp);
        await using var db = Context();
        Assert.Equal(1, await db.Occurrences.CountAsync());
        Assert.Equal(1, await db.Completions.CountAsync());
    }

    /// <summary>Verifies ownership and invalid operations fail without partial writes.</summary>
    /// <returns>A task completing after the regression checks.</returns>
    [Fact]
    public async Task OwnershipAndInvalidOperationsFailWithoutPartialWrites()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await Setup(alice);
        await Setup(bob);
        var accept = new QuestCommand(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01");
        var accepted = await Command(alice, accept);
        var id = accepted.Occurrences.Single().Occurrence.Id;
        Assert.Empty((await Read(bob)).Occurrences);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "complete", id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/commands", accept with { QuestId = "PQ-CAT-Q02" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), "accept", QuestId: "invalid"))).StatusCode);
        await using var db = Context();
        Assert.Equal(1, await db.Operations.CountAsync());
        Assert.Equal(1, await db.Occurrences.CountAsync());
        Assert.Equal(0, await db.Completions.CountAsync());
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

    /// <summary>Verifies a database failure rolls back both completion and receipt, then permits the same retry.</summary>
    /// <returns>A task completing after failure injection and recovery.</returns>
    [Fact]
    public async Task ReceiptWriteFailureRollsBackCompletionAndRetryRemainsSafe()
    {
        using var host = new Host(Connection);
        using var client = host.Client("alice");
        await Setup(client);
        var accepted = await Command(client, new(Guid.NewGuid(), "accept", QuestId: "PQ-CAT-Q01"));
        var command = new QuestCommand(Guid.NewGuid(), "complete", accepted.Occurrences.Single().Occurrence.Id);
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTestReceipt ON Operations AFTER INSERT AS BEGIN THROW 51001, 'Injected receipt failure', 1; END;");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => client.PostAsJsonAsync("/api/commands", command));
            Assert.Equal(0, await db.Completions.CountAsync());
            Assert.Equal(1, await db.Operations.CountAsync());
            Assert.Equal(1, await db.Audit.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTestReceipt;");
        }

        Assert.Equal(10, (await Command(client, command)).OverallXp);
        Assert.Equal(10, (await Command(client, command)).OverallXp);
        Assert.Equal(1, await db.Completions.CountAsync());
        Assert.Equal(2, await db.Audit.CountAsync());
    }

    private static async Task Setup(HttpClient client) => (await client.PostAsJsonAsync("/api/profile", new { Zone = "America/New_York" })).EnsureSuccessStatusCode();

    private static async Task<QuestState> Read(HttpClient client) => (await client.GetFromJsonAsync<QuestState>("/api/state", Json))!;

    private static async Task<QuestState> Command(HttpClient client, QuestCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/commands", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<QuestState>(Json))!;
    }

    private QuestDbContext Context() => new(new DbContextOptionsBuilder<QuestDbContext>().UseSqlServer(Connection).Options);

    private IdentityDbContext IdentityContext() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(Connection.Replace(database, database + "_Identity")).Options);

    private sealed class Host(string connection, bool networkIdentity = false) : WebApplicationFactory<PocketQuestsApiProgram>
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
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:quests"] = connection }));
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
