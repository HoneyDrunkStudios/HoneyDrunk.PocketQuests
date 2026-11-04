using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Domain.Errors;

namespace PocketQuests.Services.Administration.Validators;

internal static class AdminUserValidator
{
    internal const int PageSize = 50;

    internal static void Validate(SetUserAccessRequest request)
    {
        if (!CanonicalUser(request.UserId) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || request.ExpectedRevision < 0)
            throw new QuestValidationException("Choose a user, enter a reason of 1–500 characters, and reload the current account status.");
    }

    internal static bool CanonicalUser(string? userId) => userId is { Length: 30 } && userId.StartsWith("usr_", StringComparison.Ordinal)
        && userId.AsSpan(4).IndexOfAnyExcept("0123456789ABCDEFGHJKMNPQRSTVWXYZ") < 0;
}
