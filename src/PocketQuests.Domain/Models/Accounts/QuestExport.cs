using PocketQuests.Domain.Models.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Models.Accounts;

/// <summary>A consistent private snapshot; credentials, operation payloads and other accounts are excluded.</summary>
public record QuestExport(int SchemaVersion, DateTimeOffset GeneratedAt, DateTimeOffset SnapshotCutoff, Guid AccountId,
    QuestState State, ImmutableArray<QuestDefinition> DefinitionRevisions, ImmutableArray<Completion> Completions,
    ImmutableArray<UndoEvent> Undos, string Notice = "Only server-confirmed data at the snapshot cutoff is included. Unsynced device changes are excluded.");
