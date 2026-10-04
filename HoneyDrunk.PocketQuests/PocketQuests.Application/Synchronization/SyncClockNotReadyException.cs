namespace PocketQuests.Application.Synchronization;

/// <summary>The server clock cannot yet verify an immutable recorded action; retry the same payload later.</summary>
/// <param name="message">The reconciliation reason safe to display to the account owner.</param>
public sealed class SyncClockNotReadyException(string message) : Exception(message);
