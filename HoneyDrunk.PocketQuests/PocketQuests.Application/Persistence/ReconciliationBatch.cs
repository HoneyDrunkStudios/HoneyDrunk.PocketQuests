namespace PocketQuests.Application.Persistence;

/// <summary>One bounded private maintenance scan; an unfinished account returns on the next round without starving later accounts.</summary>
/// <param name="Scanned">Number of candidate accounts inspected.</param>
/// <param name="Reconciled">Number of accounts needing a source/projection check.</param>
/// <param name="Deliveries">Total due cursor steps processed across these accounts.</param>
/// <param name="Next">Next scan cursor, or null when the current round is complete.</param>
public sealed record ReconciliationBatch(int Scanned, int Reconciled, int Deliveries, ReconciliationCursor? Next);
