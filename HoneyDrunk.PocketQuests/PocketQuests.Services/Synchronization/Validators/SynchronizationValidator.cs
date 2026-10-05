using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Synchronization;

namespace PocketQuests.Services.Synchronization.Validators;

internal static class SynchronizationValidator
{
    private const int ClockSkewSeconds = 5;
    private const string RequiredIdentifiers = "Device and process identifiers are required.";
    private const string ClockNotReady = "Server time is behind committed account history.";

    internal static void RequireIdentifiers(Guid deviceId, Guid bootId)
    {
        if (deviceId == Guid.Empty || bootId == Guid.Empty)
            throw new QuestValidationException(RequiredIdentifiers);
    }

    internal static void RequireClock(DateTimeOffset floor, DateTimeOffset now)
    {
        if (floor > now.AddSeconds(ClockSkewSeconds))
            throw new SyncClockNotReadyException(ClockNotReady);
    }
}
