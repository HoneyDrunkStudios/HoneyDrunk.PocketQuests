using HoneyDrunk.Identity.Client;
using HoneyDrunk.Kernel.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Api.Authentication;
using PocketQuests.Api.Exports;
using PocketQuests.Api.Quests;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Quests;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.AccountLifecycle;
using PocketQuests.Data.Context;
using PocketQuests.Data.Repositories;
using PocketQuests.ServiceDefaults;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<QuestDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("quests") ?? throw new InvalidOperationException("SQL Server connection 'quests' is required.")));
builder.Services.AddHealthChecks().AddDbContextCheck<QuestDbContext>();
builder.Services.AddScoped<IQuestStore, SqlQuestStore>();
builder.Services.AddScoped<ISyncAnchors, SqlQuestStore>();
builder.Services.AddScoped<QuestService>();
builder.Services.AddScoped<SqlQuestLifecycle>();
builder.AddLifecycleRuntime();
builder.Services.AddHttpClient<IdentityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Identity:BaseUrl"]
        ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? "http://localhost:5218/" : throw new InvalidOperationException("Identity:BaseUrl is required outside development.")));
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing") && client.BaseAddress.Scheme != "https")
        throw new InvalidOperationException("Identity requires HTTPS outside local development.");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddAuthentication("Identity").AddScheme<AuthenticationSchemeOptions, IdentityAuthentication>("Identity", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:8081", "http://127.0.0.1:8081").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    foreach (var header in context.Request.Headers.Keys.Where(key =>
        key.StartsWith("X-Baggage-", StringComparison.OrdinalIgnoreCase) ||
        new[] { "X-Tenant-Id", "X-Project-Id", "X-Causation-Id", "baggage" }.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
    {
        context.Request.Headers.Remove(header);
    }

    context.Request.Headers["X-Correlation-Id"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    await next(context);
});
app.UseGridContext();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (UnauthorizedAccessException)
    {
        await Results.Unauthorized().ExecuteAsync(context);
    }
    catch (ArgumentException error)
    {
        await Results.Problem(error.Message, statusCode: 400).ExecuteAsync(context);
    }
    catch (InvalidOperationException)
    {
        await Results.Problem("The action conflicts with current quest state. Refresh and check the deadline or Undo window.", statusCode: 409).ExecuteAsync(context);
    }
    catch (KeyNotFoundException)
    {
        await Results.NotFound().ExecuteAsync(context);
    }
});
app.MapDefaultEndpoints();
app.MapQuestEndpoints();
app.MapExportEndpoints();
app.Run();
