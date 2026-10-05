using HoneyDrunk.Identity.Client;
using HoneyDrunk.Kernel.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Api.Authentication;
using PocketQuests.Api.Endpoints;
using PocketQuests.Api.Errors;
using PocketQuests.Api.Filters;
using PocketQuests.Api.Hosting;
using PocketQuests.Api.OpenApi;
using PocketQuests.Api.Quests;
using PocketQuests.Data;
using PocketQuests.ServiceDefaults;
using PocketQuests.Services;
using PocketQuests.Services.Accounts;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApiJson();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.AddQuestPersistence();
builder.Services.AddQuestServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAccount>(services => new CurrentAccount(services.GetRequiredService<IHttpContextAccessor>()));
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
builder.AddApiHttpPolicy();
builder.Services.AddQuestOpenApi();
var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseApiTransport();
app.UseRouting();
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
app.UseRateLimiter();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapProductEndpoints();
app.Run();
