using HoneyDrunk.Identity.Abstractions.Accounts;
using HoneyDrunk.Identity.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SqlServer.Dac;
using PocketQuests.Api;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.SchemaTests;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Runs the same product regressions on real SQL with controlled Identity HTTP responses.
/// Identity JWT/provider and cross-service lifecycle tests remain in the source-integration project.</summary>
public sealed partial class SqlApiTests : IAsyncLifetime
{
    private readonly SchemaFixture fixture = new();

    private string Connection => fixture.Connection;

    /// <inheritdoc />
    public Task InitializeAsync() => fixture.InitializeAsync();

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await DisposePersistence();
        await fixture.DisposeAsync();
    }

    private Task Republish()
    {
        using var package = DacPackage.Load(Path.Combine(AppContext.BaseDirectory, "PocketQuests.Database.dacpac"));
        fixture.Services().Deploy(package, fixture.Database, upgradeExisting: true, options: SchemaFixture.Options);
        return Task.CompletedTask;
    }

    private sealed class Host(string connection, TimeProvider? clock = null) : WebApplicationFactory<PocketQuestsApiProgram>
    {
        private readonly Dictionary<string, UserRecord> users = [];

        public HttpClient Client(string subject)
        {
            var owner = TestIdentity(subject).Subject;
            users.TryAdd(owner, new(owner, IdentityProtocol.Active, DateTimeOffset.UtcNow, 0, null));
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", owner);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:quests", connection);
            builder.UseSetting("Persistence:ReconciliationEnabled", "false");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:quests"] = connection,
                ["Persistence:ReconciliationEnabled"] = "false",
                ["Lifecycle:ServiceBusNamespace"] = null,
            }));
            builder.ConfigureServices(services =>
            {
                if (clock is not null)
                    services.AddSingleton(clock);
                services.AddHttpClient<IdentityClient>().ConfigurePrimaryHttpMessageHandler(() => new IdentityReplies(users));
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
