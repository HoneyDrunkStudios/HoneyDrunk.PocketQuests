using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Responses.Projections;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Exports;

/// <summary>The public QuestExport JSON contract, independent of storage and domain behavior.</summary>
/// <param name="SchemaVersion">The version of this serialized schema.</param>
/// <param name="GeneratedAt">The instant this snapshot was generated.</param>
/// <param name="SnapshotCutoff">The instant delimiting the retained snapshot.</param>
/// <param name="AccountId">The owning account identifier.</param>
/// <param name="State">The state represented by this result.</param>
/// <param name="DefinitionRevisions">The retained immutable quest definition revisions.</param>
/// <param name="Completions">The completion records included in this snapshot.</param>
/// <param name="Undos">The retained undo events included in this snapshot.</param>
/// <param name="Notice">The notice associated with this result.</param>
/// <param name="SavedPlaces">The account saved places included in this snapshot.</param>
public sealed record QuestExport(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset SnapshotCutoff,
    Guid AccountId,
    QuestState State,
    ImmutableArray<QuestDefinition> DefinitionRevisions,
    ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos,
    string Notice,
    ImmutableArray<PocketQuests.Contracts.Models.Discoveries.SavedPlace> SavedPlaces);
