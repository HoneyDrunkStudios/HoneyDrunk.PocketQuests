using PocketQuests.Api.Contracts.Catalogs;
using PocketQuests.Api.Contracts.Commands;
using PocketQuests.Api.Contracts.Common;
using PocketQuests.Api.Contracts.Exports;
using PocketQuests.Api.Contracts.Profiles;
using PocketQuests.Api.Contracts.Progress;
using PocketQuests.Api.Contracts.Projections;
using PocketQuests.Api.Contracts.Quests;
using PocketQuests.Api.Contracts.Schedules;
using PocketQuests.Api.Contracts.Synchronization;
using System.Collections.Immutable;

namespace PocketQuests.Api.Contracts.Exports;

/// <summary>Explicit mappings for the exports HTTP contracts.</summary>
public static class ExportsContractMapping
{
    /// <summary>Maps QuestExport explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Api.Contracts.Exports.QuestExport ToContract(this PocketQuests.Domain.Models.Accounts.QuestExport value) => new(
        value.SchemaVersion,
        value.GeneratedAt,
        value.SnapshotCutoff,
        value.AccountId,
        value.State.ToContract(),
        value.DefinitionRevisions.Select(item => item.ToContract()).ToImmutableArray(),
        value.Completions.Select(item => item.ToContract()).ToImmutableArray(),
        value.Undos.Select(item => item.ToContract()).ToImmutableArray(),
        value.Notice);
}
