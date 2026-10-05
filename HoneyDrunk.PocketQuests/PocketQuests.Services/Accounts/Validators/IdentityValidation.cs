using PocketQuests.Domain.Models.Accounts;

namespace PocketQuests.Services.Accounts.Validators;

internal static class IdentityValidation
{
    private const int IdentifierLength = 30;
    private const string IdentifierPrefix = "usr_";
    private const string InvalidIdentity = "The trusted resolver must supply a canonical Identity usr_ identifier.";
    private static readonly System.Buffers.SearchValues<char> CanonicalUserCharacters = System.Buffers.SearchValues.Create("0123456789ABCDEFGHJKMNPQRSTVWXYZ");

    internal static void RequireCanonical(AccountIdentity identity)
    {
        if (identity.Subject is null || identity.Subject.Length != IdentifierLength || !identity.Subject.StartsWith(IdentifierPrefix, StringComparison.Ordinal)
            || identity.Subject.AsSpan(IdentifierPrefix.Length).ContainsAnyExcept(CanonicalUserCharacters))
            throw new ArgumentException(InvalidIdentity);
    }
}
