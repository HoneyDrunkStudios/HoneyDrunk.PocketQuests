using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using System.Collections.Immutable;

namespace PocketQuests.Application.Exports;

/// <summary>A consistent private snapshot; credentials, operation payloads and other accounts are excluded.</summary>
public record QuestExport(int SchemaVersion, DateTimeOffset GeneratedAt, DateTimeOffset SnapshotCutoff, Guid AccountId,
    QuestState State, ImmutableArray<QuestDefinition> DefinitionRevisions, ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos, string Notice = "Only server-confirmed data at the snapshot cutoff is included. Unsynced device changes are excluded.");
