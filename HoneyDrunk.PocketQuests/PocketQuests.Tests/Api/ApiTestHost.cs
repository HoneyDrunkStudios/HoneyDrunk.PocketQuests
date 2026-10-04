using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PocketQuests.Api.Contracts;
using PocketQuests.Api.Errors;
using PocketQuests.Api.Exports;
using PocketQuests.Api.Hosting;
using PocketQuests.Api.OpenApi;
using PocketQuests.Api.Quests;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Quests;
using PocketQuests.Application.Synchronization;
using System.Net;
using System.Net.Http.Headers;

namespace PocketQuests.Tests.Api;

internal sealed class ApiTestHost(WebApplication app, HttpClient client, ApiTestStore store, ApiTestLog logs) : IAsyncDisposable
{
    internal WebApplication App { get; } = app;

    internal HttpClient Client { get; } = client;

    internal ApiTestStore Store { get; } = store;

    internal ApiTestLog Logs { get; } = logs;

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }

    internal static async Task<ApiTestHost> Start(string environment = "Testing", Dictionary<string, string?>? configuration = null, IPAddress? remote = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            ApplicationName = typeof(QuestEndpoints).Assembly.GetName().Name,
        });
        builder.Configuration.AddInMemoryCollection(configuration ?? []);
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_ => new ApiTestLog());
        builder.Services.AddSingleton<ILoggerProvider>(services => services.GetRequiredService<ApiTestLog>());
        builder.Services.AddApiJson();
        builder.Services.AddSingleton(TimeProvider.System);
        var store = new ApiTestStore();
        builder.Services.AddSingleton<IQuestStore>(store);
        builder.Services.AddSingleton<ISyncAnchors>(store);
        builder.Services.AddScoped<QuestService>();
        builder.Services.AddTransient(services => new ApiTestAuthentication(
            services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions>>(),
            services.GetRequiredService<ILoggerFactory>(),
            System.Text.Encodings.Web.UrlEncoder.Default));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ApiTestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.AddApiHttpPolicy();
        builder.Services.AddQuestOpenApi();
        var app = builder.Build();
        var logs = app.Services.GetRequiredService<ApiTestLog>();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = remote ?? IPAddress.Parse("192.0.2.99");
            await next(context);
        });
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseApiTransport();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapQuestEndpoints();
        app.MapExportEndpoints();
        app.MapOpenApi();
        await app.StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://api.example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-a");
        return new(app, client, store, logs);
    }
}
