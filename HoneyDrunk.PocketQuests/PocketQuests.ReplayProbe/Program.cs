using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services;
using PocketQuests.Domain.Services.Quests;
using System.Globalization;
using System.Text.Json;

// Test-only executable. Refuse any connection outside the isolated schema runner's databases.
var connection = Environment.GetEnvironmentVariable("POCKETQUESTS_REPLAY_PROBE_CONNECTION") ?? throw new InvalidOperationException("A disposable fixture connection is required.");
var parsed = new SqlConnectionStringBuilder(connection);
if (!parsed.IntegratedSecurity || !parsed.DataSource.StartsWith("(localdb)\\PQSchema_", StringComparison.Ordinal) || !parsed.InitialCatalog.StartsWith("PocketQuests_SchemaTests_", StringComparison.Ordinal))
    throw new InvalidOperationException("This probe accepts only disposable schema test databases.");
if (args.Length != 3)
    throw new ArgumentException("Provide the synthetic command path, canonical test user and replay instant.");
var command = JsonSerializer.Deserialize<QuestCommand>(await File.ReadAllTextAsync(args[0])) ?? throw new ArgumentException("Command is missing.");
var services = new ServiceCollection();
services.AddQuestDataServices(connection);
services.AddQuestBusinessServices();
await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
await using var scope = provider.CreateAsyncScope();
var state = await scope.ServiceProvider.GetRequiredService<IQuestService>().Execute(new AccountIdentity("verified-identity", args[1]), command, DateTimeOffset.Parse(args[2], CultureInfo.InvariantCulture));
Console.WriteLine(JsonSerializer.Serialize(state));
