using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("This AppHost starts local development tools. Run the API and Identity service directly for other environments with explicit configuration.");
builder.Configuration.AddJsonFile(Path.GetFullPath("../../.local/development.json", builder.AppHostDirectory), optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
var api = builder.AddProject<Projects.PocketQuests_Api>("api")
    .WithReference(builder.AddConnectionString("quests"))
    .WithHttpHealthCheck("/health");
var deviceHost = builder.Configuration["Mobile:DeviceHost"] ?? "localhost";
if (Uri.CheckHostName(deviceHost) is not (UriHostNameType.Dns or UriHostNameType.IPv4))
    throw new InvalidOperationException("Mobile:DeviceHost must be a DNS name or IPv4 address.");

var identityUrl = builder.Configuration["Identity:ServiceUrl"];
if (!string.IsNullOrWhiteSpace(identityUrl))
{
    var address = new Uri(identityUrl, UriKind.Absolute);
    if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
        throw new InvalidOperationException("An external Identity service must use HTTPS.");
    var externalIdentity = builder.AddExternalService("identity-api", address).WithHttpHealthCheck("/health");
    api.WithEnvironment("Identity__BaseUrl", identityUrl).WaitFor(externalIdentity);
}
else
{
    var identitySource = builder.Configuration["Identity:SourceRoot"]
        ?? throw new InvalidOperationException("Configure Identity:ServiceUrl, or run scripts/Initialize-Local.ps1 with an explicit Identity source checkout.");
    var identityProject = Path.Combine(identitySource, "HoneyDrunk.Identity", "HoneyDrunk.Identity.Api", "HoneyDrunk.Identity.Api.csproj");
    if (!File.Exists(identityProject))
        throw new InvalidOperationException("The configured Identity source project does not exist.");
    var identity = builder.AddProject("identity-api", identityProject)
        .WithReference(builder.AddConnectionString("identity"))
        .WithHttpHealthCheck("/health");
    api.WithEnvironment("Identity__BaseUrl", identity.GetEndpoint("http")).WaitFor(identity);
    identityUrl = $"http://{deviceHost}:5218";
}

// Device addresses describe the caller's route to the Aspire proxy. A physical
// phone additionally needs reachable bindings and trusted transport configured.
var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "npm";
string[] arguments = OperatingSystem.IsWindows()
    ? ["/d", "/c", "npm", "run", "start", "--", "--port", "8081"]
    : ["run", "start", "--", "--port", "8081"];
builder.AddExecutable("mobile", executable, "../../apps/mobile", arguments)
    .WaitFor(api)
    .WithEnvironment("EXPO_PUBLIC_API_URL", $"http://{deviceHost}:5217")
    .WithEnvironment("EXPO_PUBLIC_IDENTITY_URL", identityUrl)
    .WithEnvironment("EXPO_NO_TELEMETRY", "1")
    .WithHttpEndpoint(port: 8081, targetPort: 8081, isProxied: false);
builder.Build().Run();
