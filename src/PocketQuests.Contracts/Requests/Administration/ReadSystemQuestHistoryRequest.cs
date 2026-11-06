namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Reads immutable revisions before an exclusive version cursor; null starts at the newest revision.</summary>
/// <param name="Id">The system quest identifier.</param>
/// <param name="Before">The exclusive revision cursor; null requests the newest page.</param>
public sealed record ReadSystemQuestHistoryRequest(Guid Id, int? Before = null);
