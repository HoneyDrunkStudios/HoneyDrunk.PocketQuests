using PocketQuests.Domain.Models.Accounts;

namespace PocketQuests.Services.Accounts.Validators;

internal static class IdentityValidation
{
    private static readonly System.Buffers.SearchValues<char> CanonicalUserCharacters = System.Buffers.SearchValues.Create("0123456789ABCDEFGHJKMNPQRSTVWXYZ");

    internal static void RequireCanonical(AccountIdentity identity)
    {
        if (identity.Subject.Length != 30 || !identity.Subject.StartsWith("usr_", StringComparison.Ordinal)
            || identity.Subject.AsSpan(4).ContainsAnyExcept(CanonicalUserCharacters))
            throw new ArgumentException("The trusted resolver must supply a canonical Identity usr_ identifier.");
    }
}
