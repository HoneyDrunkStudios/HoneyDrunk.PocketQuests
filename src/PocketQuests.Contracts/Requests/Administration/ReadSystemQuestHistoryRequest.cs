namespace PocketQuests.Contracts.Requests.Administration;

/// <summary>Reads immutable revisions before an exclusive version cursor; null starts at the newest revision.</summary>
public sealed record ReadSystemQuestHistoryRequest(Guid Id, int? Before = null);
