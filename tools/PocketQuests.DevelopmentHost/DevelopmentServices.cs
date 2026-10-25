using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Identity.Abstractions.Authentication;
using HoneyDrunk.Identity.Api;
using HoneyDrunk.Identity.Client;
using HoneyDrunk.Identity.Providers.Entra.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using PocketQuests.Api;

namespace PocketQuests.DevelopmentHost;

internal sealed class DevelopmentServices(string questsConnection, string identityConnection, DevelopmentIdentity identity) : IDisposable
{
    private readonly IdentityFactory identityHost = new(identityConnection, identity);
    private QuestFactory? questHost;

    internal HttpClient IdentityClient => identityHost.CreateClient();

    internal HttpClient QuestClient => (questHost ??= new(questsConnection, identityHost)).CreateClient();

    public void Dispose()
    {
        questHost?.Dispose();
        identityHost.Dispose();
    }

    private sealed class IdentityFactory(string connection, DevelopmentIdentity identity) : WebApplicationFactory<IdentityApiProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:identity", connection);
            builder.UseSetting("Entra:Graph:CredentialMode", "ManagedIdentity");
            builder.UseSetting("Lifecycle:ServiceBusNamespace", string.Empty);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:identity"] = connection,
                ["Entra:Authority"] = null,
                ["Entra:Graph:CredentialMode"] = "ManagedIdentity",
                ["Lifecycle:ServiceBusNamespace"] = null,
            }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new EntraSigningKeys(new ConfigurationBuilder().Build()));
                services.AddSingleton<ISigningKeyProvider>(identity);
                services.AddSingleton<IExternalAccounts>(identity);
            });
        }
    }

    private sealed class QuestFactory(string connection, IdentityFactory identityHost) : WebApplicationFactory<PocketQuestsApiProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:quests", connection);

            // Retained accounts need the normal worker to drain recurrence backlogs.
            builder.UseSetting("Persistence:ReconciliationEnabled", "true");
            builder.UseSetting("Lifecycle:ServiceBusNamespace", string.Empty);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:quests"] = connection,
                ["Persistence:ReconciliationEnabled"] = "true",
                ["Lifecycle:ServiceBusNamespace"] = null,
            }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddHttpClient<IdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(() => identityHost.Server.CreateHandler()));
        }
    }
}
