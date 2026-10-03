using PocketQuests.Tests.Fixtures;
using System.Text.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Opt-in loopback browser fixture using real SQL, API and Identity with test-only token keys.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Runs isolated real services until the browser writes its completion marker or the bound expires.</summary>
    /// <param name="directory">Ignored fixture directory containing a short-lived test token.</param>
    /// <returns>The bounded fixture lifetime.</returns>
    internal static async Task RunBrowserFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        var ready = Path.Combine(directory, "ready.json");
        var done = Path.Combine(directory, "done");
        File.Delete(ready);
        File.Delete(done);
        var fixture = new SqlApiTests();
        try
        {
            await fixture.InitializeAsync();
            using var host = new Host(fixture.Connection, networkIdentity: true);
            host.UseKestrel(5217);
            using var client = host.Client("browser-user");
            await using var provider = await NativeTestProvider.Start(client.DefaultRequestHeaders.Authorization!.Parameter!);
            await File.WriteAllTextAsync(ready, JsonSerializer.Serialize(new { token = client.DefaultRequestHeaders.Authorization!.Parameter }));
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(8);
            while (!File.Exists(done) && DateTimeOffset.UtcNow < cutoff)
                await Task.Delay(500);
            if (!File.Exists(done))
                throw new TimeoutException("Browser verification did not finish within eight minutes.");
        }
        finally
        {
            File.Delete(ready);
            await fixture.DisposeAsync();
        }
    }
}
