using PocketQuests.AppHost;

namespace PocketQuests.Tests.Unit;

/// <summary>Verifies local transport cannot silently become plaintext LAN access.</summary>
public sealed class MobileEndpointTests
{
    /// <summary>Local browser and Android bridge defaults remain usable.</summary>
    /// <param name="host">The local host name.</param>
    /// <param name="expected">The expected service address.</param>
    [Theory]
    [InlineData("localhost", "http://localhost:5217")]
    [InlineData("127.0.0.1", "http://127.0.0.1:5217")]
    public void LocalDefaults(string host, string expected) =>
        Assert.Equal(expected, MobileEndpoint.Resolve(null, host, 5217));

    /// <summary>Emulator transport must be an explicitly trusted development route.</summary>
    [Fact]
    public void EmulatorBridgeRequiresExplicitTrust()
    {
        Assert.Throws<InvalidOperationException>(() => MobileEndpoint.Resolve(null, "10.0.2.2", 5217));
        Assert.Equal("http://10.0.2.2:5217", MobileEndpoint.Resolve(null, "10.0.2.2", 5217, "10.0.2.2"));
        Assert.Throws<InvalidOperationException>(() => MobileEndpoint.Resolve(null, "192.168.1.20", 5217, "10.0.2.2"));
    }

    /// <summary>Remote hosts cannot inherit the local plaintext defaults.</summary>
    /// <param name="host">The remote host name.</param>
    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("localhost.example.com")]
    public void RemoteDefaultRequiresHttps(string host) =>
        Assert.Throws<InvalidOperationException>(() => MobileEndpoint.Resolve(null, host, 5217));

    /// <summary>Configured transport rejects plaintext and embedded credentials.</summary>
    /// <param name="url">The unsafe service address.</param>
    [Theory]
    [InlineData("http://192.168.1.20:5217")]
    [InlineData("http://localhost:5217")]
    [InlineData("https://user:password@example.invalid")]
    [InlineData("https://example.invalid/?token=secret")]
    [InlineData("https://example.invalid/#fragment")]
    public void UnsafeConfiguredEndpointIsRejected(string url) =>
        Assert.Throws<InvalidOperationException>(() => MobileEndpoint.Resolve(url, "localhost", 5217));

    /// <summary>Explicit trusted HTTPS routing supports a physical device.</summary>
    [Fact]
    public void PhysicalDeviceUsesConfiguredHttps() =>
        Assert.Equal("https://dev.example.invalid/api", MobileEndpoint.Resolve("https://dev.example.invalid/api/", "192.168.1.20", 5217));
}
