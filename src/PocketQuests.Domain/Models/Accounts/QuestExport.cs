using PocketQuests.Domain.Models.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Accounts;

/// <summary>A consistent private snapshot; credentials, operation payloads and other accounts are excluded.</summary>
/// <param name="SchemaVersion">The version of this serialized schema.</param>
/// <param name="GeneratedAt">The instant this snapshot was generated.</param>
/// <param name="SnapshotCutoff">The instant delimiting the retained snapshot.</param>
/// <param name="AccountId">The owning account identifier.</param>
/// <param name="State">The state represented by this result.</param>
/// <param name="DefinitionRevisions">The retained immutable quest definition revisions.</param>
/// <param name="Completions">The completion records included in this snapshot.</param>
/// <param name="Undos">The retained undo events included in this snapshot.</param>
/// <param name="Notice">The notice associated with this result.</param>
public record QuestExport(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset SnapshotCutoff,
    Guid AccountId,
    QuestState State,
    ImmutableArray<QuestDefinition> DefinitionRevisions,
    ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos,
    string Notice = "Only server-confirmed data at the snapshot cutoff is included. Unsynced device changes are excluded.")
{
    /// <summary>Gets saved places from the same private export transaction.</summary>
    public ImmutableArray<PocketQuests.Domain.Models.Discoveries.SavedPlace> SavedPlaces { get; init; } = [];
}
