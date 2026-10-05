namespace PocketQuests.Data.Queries.Quests;

/// <summary>Custom definition terms and archive source references for a private export.</summary>
/// <param name="Terms">Retained custom definition revisions and allocations.</param>
/// <param name="ArchivedRevisionIds">Revisions targeted by committed archive commands.</param>
public sealed record QuestDefinitionHistoryRows(QuestTermsRows Terms, IReadOnlyList<Guid> ArchivedRevisionIds);
