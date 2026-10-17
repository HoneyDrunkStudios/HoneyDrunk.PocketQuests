using PocketQuests.Contracts.Models.Quests;
using PocketQuests.Contracts.Responses.Projections;
using System.Collections.Immutable;

namespace PocketQuests.Contracts.Responses.Exports;

/// <summary>The public QuestExport JSON contract, independent of storage and domain behavior.</summary>
public sealed record QuestExport(int SchemaVersion, DateTimeOffset GeneratedAt, DateTimeOffset SnapshotCutoff, Guid AccountId,
    QuestState State, ImmutableArray<QuestDefinition> DefinitionRevisions, ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos, string Notice);
