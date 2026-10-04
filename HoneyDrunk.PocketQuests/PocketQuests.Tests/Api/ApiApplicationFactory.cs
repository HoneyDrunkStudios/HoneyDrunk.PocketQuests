using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PocketQuests.Api;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using System.Text.Encodings.Web;

namespace PocketQuests.Tests.Api;

internal sealed class ApiApplicationFactory : WebApplicationFactory<PocketQuestsApiProgram>
{
    internal ApiTestStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Persistence:ReconciliationEnabled", "false");
        builder.UseSetting("Lifecycle:ServiceBusNamespace", string.Empty);
        builder.UseSetting("ConnectionStrings:quests", "Server=127.0.0.1;Database=NotUsedByOpenApi;Integrated Security=true;Encrypt=true");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IQuestStore>(Store);
            services.AddSingleton<ISyncAnchors>(Store);
            services.AddTransient(provider => new ApiTestAuthentication(
                provider.GetRequiredService<IOptionsMonitor<AuthenticationSchemeOptions>>(),
                provider.GetRequiredService<ILoggerFactory>(),
                UrlEncoder.Default));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ApiTestAuthentication>("Test", _ => { });
        });
    }
}
