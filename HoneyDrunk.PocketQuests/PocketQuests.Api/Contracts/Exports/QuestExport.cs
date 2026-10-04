using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests.Definitions;
using PocketQuests.Api.Contracts.Quests.Events;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts.Exports;

/// <summary>The public QuestExport JSON contract, independent of storage and domain behavior.</summary>
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record QuestExport(int SchemaVersion, DateTimeOffset GeneratedAt, DateTimeOffset SnapshotCutoff, Guid AccountId,
    QuestState State, ImmutableArray<QuestDefinition> DefinitionRevisions, ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos, string Notice);
