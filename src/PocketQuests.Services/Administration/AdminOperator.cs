using Microsoft.Extensions.Options;
using PocketQuests.Services.Accounts;

namespace PocketQuests.Services.Administration;

/// <summary>Enforces the configured canonical operator at the service boundary as well as HTTP.</summary>
/// <param name="current">Identity verified by the host.</param>
/// <param name="settings">The explicitly configured operator identifier.</param>
public sealed class AdminOperator(ICurrentAccount current, IOptions<AdminAccessOptions> settings)
{
    /// <summary>Returns the verified operator or rejects the operation before reading another profile.</summary>
    /// <returns>The verified canonical operator user identifier.</returns>
    public string Require()
    {
        var identity = current.Identity;
        if (string.IsNullOrEmpty(settings.Value.UserId) || identity.Issuer != "honeydrunk-identity"
            || !string.Equals(identity.Subject, settings.Value.UserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Administrative access is not configured for this account.");
        return identity.Subject;
    }
}
