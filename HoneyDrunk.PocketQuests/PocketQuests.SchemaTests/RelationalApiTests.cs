using HoneyDrunk.Identity.Abstractions.Accounts;
using HoneyDrunk.Identity.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Api;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.AccountLifecycle;
using PocketQuests.Data.Relational;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Data.Repositories;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.SchemaTests;

/// <summary>Real API and SQL with controlled Identity HTTP replies; not live provider/JWT/broker validation.</summary>
/// <param name="fixture">Explicitly isolated GUID database.</param>
public sealed partial class RelationalApiTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Both persistence selections compose consistently and invalid configuration fails startup.</summary>
    /// <param name="mode">Explicit selection, or omitted legacy-compatible default.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Legacy")]
    [InlineData("Relational")]
    [InlineData("Invalid")]
    public void ExplicitModeSelectsStoreAnchorsAndLifecycleTogether(string? mode)
    {
        using var host = new Host(fixture.Connection, mode: mode);
        if (mode == "Invalid")
        {
            Assert.Contains("Persistence:Mode", Assert.Throws<InvalidOperationException>(() => host.CreateClient()).Message, StringComparison.Ordinal);
            return;
        }

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        if (mode == "Relational")
        {
            Assert.IsType<RelationalQuestStore>(services.GetRequiredService<IQuestStore>());
            Assert.Same(services.GetRequiredService<IQuestStore>(), services.GetRequiredService<ISyncAnchors>());
            Assert.IsType<RelationalQuestLifecycle>(services.GetRequiredService<IQuestLifecycle>());
        }
        else
        {
            Assert.IsType<SqlQuestStore>(services.GetRequiredService<IQuestStore>());
            Assert.IsType<SqlQuestStore>(services.GetRequiredService<ISyncAnchors>());
            Assert.IsType<SqlQuestLifecycle>(services.GetRequiredService<IQuestLifecycle>());
        }
    }

    /// <summary>GET never initializes or writes and does not wait on the account application lock.</summary>
    /// <returns>Completion after HTTP and unchanged rowversion checks.</returns>
    [Fact]
    public async Task ExplicitInitializationAndReadOnlyStatePreserveOwnershipAndWireShape()
    {
        using var host = new Host(fixture.Connection);
        using var alice = host.Client();
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        await using var db = fixture.Context();
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == host.Owner));
        var initialized = await Setup(alice);
        Assert.Equal("America/New_York", initialized.Zone);
        var accepted = await Command(alice, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"));
        using var bob = host.Client(NewOwner());
        await Setup(bob);
        Assert.Empty((await Read(bob)).Occurrences);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accepted.Occurrences.Single().Occurrence.Id), Json)).StatusCode);
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == host.Owner);
        await using var connection = new SqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var locked = new SqlCommand("pocketquests.AcquireAccountLock", connection, transaction) { CommandType = CommandType.StoredProcedure };
        locked.Parameters.Add("@IdentityUserId", SqlDbType.VarChar, 30).Value = host.Owner;
        await locked.ExecuteNonQueryAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await alice.GetAsync(new Uri("/api/state", UriKind.Relative), timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal("Active", document.RootElement.GetProperty("occurrences")[0].GetProperty("status").GetString());
        Assert.Equal(JsonSerializer.SerializeToElement(accepted with { CompletionOutcome = null }, Json).EnumerateObject().Select(p => p.Name), document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
        await transaction.RollbackAsync();
    }

    /// <summary>Independent hosts preserve exact receipt feedback, Undo and conflict status without double reward.</summary>
    /// <param name="mode">Explicit persistence selection.</param>
    /// <returns>Completion after real host restart and concurrent retry.</returns>
    [Theory]
    [InlineData("Legacy")]
    [InlineData("Relational")]
    public async Task RestartReplayUndoAndConflictingPayloadKeepExistingHttpContract(string mode)
    {
        var owner = NewOwner();
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01");
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        string original;
        using (var host = new Host(fixture.Connection, owner, mode))
        using (var client = host.Client())
        {
            await Setup(client);
            await Command(client, accept);
            using var response = await client.PostAsJsonAsync("/api/commands", complete, Json);
            response.EnsureSuccessStatusCode();
            original = await response.Content.ReadAsStringAsync();
            Assert.Equal(10, JsonSerializer.Deserialize<QuestState>(original, Json)!.OverallXp);
        }

        using (var host = new Host(fixture.Connection, owner, mode))
        using (var otherHost = new Host(fixture.Connection, owner, mode))
        using (var client = host.Client())
        using (var other = otherHost.Client())
        {
            var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/commands", complete, Json), other.PostAsJsonAsync("/api/commands", complete, Json));
            foreach (var response in responses)
            {
                using (response)
                    Assert.Equal(original, await response.Content.ReadAsStringAsync());
            }

            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/commands", accept with { QuestId = "PQ-CAT-Q02" }, Json)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/commands", new QuestCommand(Guid.NewGuid(), QuestActions.Accept, QuestId: "invalid"), Json)).StatusCode);
            Assert.Equal(0, (await Command(client, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId))).OverallXp);
            Assert.Equal(original, await (await client.PostAsJsonAsync("/api/commands", complete, Json)).Content.ReadAsStringAsync());
        }

        using var restarted = new Host(fixture.Connection, owner, mode);
        using var final = restarted.Client();
        Assert.Equal(0, (await Read(final)).OverallXp);
    }

    private static string NewOwner() => "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant();

    private static async Task<QuestState> Setup(HttpClient client) => await State(await client.PostAsJsonAsync("/api/profile", new { Zone = "America/New_York" }));

    private static async Task<QuestState> Read(HttpClient client) => await State(await client.GetAsync(new Uri("/api/state", UriKind.Relative)));

    private static async Task<QuestState> Command(HttpClient client, QuestCommand command) => await State(await client.PostAsJsonAsync("/api/commands", command, Json));

    private static async Task<QuestState> State(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<QuestState>(Json))!;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Host(string connection, string? owner = null, string? mode = "Relational", bool reconciliationEnabled = false) : WebApplicationFactory<PocketQuestsApiProgram>
    {
        public string Owner { get; } = owner ?? NewOwner();

        public Clock Clock { get; } = new();

        public Dictionary<string, UserRecord> Users { get; } = [];

        public HttpClient Client(string? user = null)
        {
            user ??= Owner;
            Users.TryAdd(user, new(user, IdentityProtocol.Active, Start, 0, null));
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", user);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Minimal-host registration reads these settings before ConfigureAppConfiguration callbacks.
            if (mode is not null)
                builder.UseSetting("Persistence:Mode", mode);
            builder.UseSetting("Persistence:ReconciliationEnabled", reconciliationEnabled.ToString());
            builder.UseSetting("ConnectionStrings:quests", connection);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:quests"] = connection,
                ["Persistence:Mode"] = mode,
                ["Persistence:ReconciliationEnabled"] = reconciliationEnabled.ToString(),
                ["Lifecycle:ServiceBusNamespace"] = null,
            }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddHttpClient<IdentityClient>().ConfigurePrimaryHttpMessageHandler(() => new IdentityReplies(Users));
            });
        }
    }

    private sealed class IdentityReplies(Dictionary<string, UserRecord> users) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/users/me", request.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Get, request.Method);
            var owner = request.Headers.Authorization?.Parameter;
            return Task.FromResult(owner is not null && users.TryGetValue(owner, out var user)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(user) }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}
