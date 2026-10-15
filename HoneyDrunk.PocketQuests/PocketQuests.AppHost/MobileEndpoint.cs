using System.Net;

namespace PocketQuests.AppHost;

internal static class MobileEndpoint
{
    internal static string Resolve(string? configured, string host, int port, string? trustedEmulatorHost = null)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo) ||
                !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
                throw new InvalidOperationException("Configured mobile service URLs must use HTTPS without credentials, query or fragment.");
            return endpoint.AbsoluteUri.TrimEnd('/');
        }

        // Plaintext is limited to this machine and Android's host-loopback bridge.
        // Physical devices must configure a trusted HTTPS service URL explicitly.
        var local = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(trustedEmulatorHost) && host.Equals(trustedEmulatorHost, StringComparison.OrdinalIgnoreCase)) ||
            (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
        if (!local)
            throw new InvalidOperationException("Physical-device access requires explicit Mobile:ApiUrl and Mobile:IdentityUrl HTTPS endpoints.");
        return new UriBuilder(Uri.UriSchemeHttp, host, port).Uri.GetLeftPart(UriPartial.Authority);
    }
}
