namespace PocketQuests.Domain.Models.Quests;

/// <summary>Due recurrence is being materialized in bounded batches; retry the exact pending command without consuming its proof.</summary>
/// <param name="message">The transient reconciliation status.</param>
public sealed class ReconciliationPendingException(string message) : Exception(message);
