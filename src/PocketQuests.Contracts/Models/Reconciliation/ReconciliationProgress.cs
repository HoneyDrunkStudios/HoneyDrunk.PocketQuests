namespace PocketQuests.Contracts.Models.Reconciliation;

/// <summary>A bounded step through due recurrence cursors, preserving the existing global delivery order.</summary>
/// <param name="Processed">Number of cursor deliveries processed in this step.</param>
/// <param name="HasMore">Whether due deliveries remain at the requested reconciliation clock.</param>
public sealed record ReconciliationProgress(int Processed, bool HasMore);
