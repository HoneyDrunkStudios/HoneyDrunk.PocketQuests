using Microsoft.AspNetCore.RateLimiting;
using PocketQuests.DevelopmentHost;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment() || builder.Configuration["DevelopmentSignIn:Enabled"] != "true")
    throw new InvalidOperationException("This tool requires Development and explicit DevelopmentSignIn:Enabled=true. It cannot run as a deployed API.");
var pairingCode = builder.Configuration["DevelopmentSignIn:PairingCode"]
    ?? throw new InvalidOperationException("Configure the development pairing code using user secrets or a private environment variable.");
var identity = new DevelopmentIdentity(pairingCode);
var instance = builder.Configuration["DevelopmentSignIn:SqlInstance"] ?? "PocketQuests";
if (instance != "PocketQuests" && !System.Text.RegularExpressions.Regex.IsMatch(instance, "^PQExpoGo_[0-9a-f]{12}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
    throw new InvalidOperationException("Use the dedicated PocketQuests instance or a generated disposable Expo Go test instance.");

// Remote devices use an explicitly configured trusted HTTPS reverse proxy.
// Never accept a caller-supplied upstream, connection string, subject or user ID.
builder.WebHost.UseUrls("http://127.0.0.1:15219");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:8081", "http://127.0.0.1:8081").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("pairing", policy =>
{
    policy.PermitLimit = 10;
    policy.Window = TimeSpan.FromMinutes(1);
    policy.QueueLimit = 0;
    policy.AutoReplenishment = true;
}).RejectionStatusCode = StatusCodes.Status429TooManyRequests);
await DevelopmentDatabase.EnsureCreated("PocketQuests_ExpoGo", "PocketQuests.Database", instance);
await DevelopmentDatabase.EnsureCreated("PocketQuests_ExpoGo_Identity", "HoneyDrunk.Identity.Database", instance);
using var services = new DevelopmentServices(DevelopmentDatabase.Connection("PocketQuests_ExpoGo", instance), DevelopmentDatabase.Connection("PocketQuests_ExpoGo_Identity", instance), identity);
using var identityClient = services.IdentityClient;
using var questClient = services.QuestClient;
var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
var instanceId = Guid.NewGuid().ToString("N");
app.MapGet("/health", () => Results.Ok(new { mode = "development", persistent = true, instanceId }));
app.MapPost("/development/sign-in", (PairingRequest request, HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return identity.Accepts(request.PairingCode)
        ? Results.Ok(new { token = identity.IssueToken() })
        : Results.Unauthorized();
}).RequireRateLimiting("pairing");
app.MapMethods("/api/{**path}", ["GET", "POST"], context => DevelopmentProxy.Forward(context, questClient, context.Request.Path + context.Request.QueryString));
app.MapGet("/identity/users/me", context => DevelopmentProxy.Forward(context, identityClient, "/users/me"));
app.MapGet("/identity/users/me/status", context => DevelopmentProxy.Forward(context, identityClient, "/users/me/status"));
await app.RunAsync();
