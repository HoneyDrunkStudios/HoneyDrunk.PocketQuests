using HoneyDrunk.Auth.Secrets;
using HoneyDrunk.Identity.Abstractions.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.DevelopmentHost;

// This provider exists only in the opt-in local tooling executable. Neither
// product API references this assembly or accepts its signing keys in deployment.
internal sealed class DevelopmentIdentity : ISigningKeyProvider, IExternalAccounts
{
    internal const string Issuer = "https://pocketquests.development.invalid";
    internal const string Audience = "pocketquests-development";
    internal const string Subject = "expo-go-personal-test-account";
    private readonly SymmetricSecurityKey key = new(RandomNumberGenerator.GetBytes(64));
    private readonly byte[] pairingHash;

    internal DevelopmentIdentity(string pairingCode)
    {
        if (pairingCode.Length != 6 || pairingCode.Any(character => character is < '0' or > '9'))
            throw new InvalidOperationException("The development pairing code must contain exactly six digits.");
        pairingHash = SHA256.HashData(Encoding.UTF8.GetBytes(pairingCode));
    }

    public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SecurityKey>>([key]);

    public Task<string> GetIssuerAsync(CancellationToken cancellationToken = default) => Task.FromResult(Issuer);

    public Task<string> GetAudienceAsync(CancellationToken cancellationToken = default) => Task.FromResult(Audience);

    public Task<TimeSpan> GetClockSkewAsync(CancellationToken cancellationToken = default) => Task.FromResult(TimeSpan.Zero);

    public Task<bool> Exists(ExternalSubject subject, CancellationToken token) => Task.FromResult(subject.Issuer == Issuer && subject.Subject == Subject);

    public Task Revoke(ExternalSubject subject, CancellationToken token) => Task.FromException(new NotSupportedException("Use real provider sign-in to test account lifecycle operations."));

    public Task Erase(ExternalSubject subject, CancellationToken token) => Task.FromException(new NotSupportedException("Use real provider sign-in to test account lifecycle operations."));

    internal bool Accepts(string? pairingCode) => pairingCode is not null &&
        CryptographicOperations.FixedTimeEquals(pairingHash, SHA256.HashData(Encoding.UTF8.GetBytes(pairingCode)));

    internal string IssueToken() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Subject = new ClaimsIdentity([
            new("sub", Subject),
            new("scp", "access_as_user"),
            new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ]),
        Expires = DateTime.UtcNow.AddHours(8),
        SigningCredentials = new(key, SecurityAlgorithms.HmacSha256),
    });
}
