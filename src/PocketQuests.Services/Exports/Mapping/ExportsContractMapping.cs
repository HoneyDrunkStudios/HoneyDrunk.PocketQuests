using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Services.Common.Mapping;
using PocketQuests.Services.Exports.Mapping;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Progress.Mapping;
using PocketQuests.Services.Projections.Mapping;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Schedules.Mapping;
using PocketQuests.Services.Synchronization.Mapping;
using System.Collections.Immutable;

namespace PocketQuests.Services.Exports.Mapping;

/// <summary>Explicit mappings for the exports HTTP contracts.</summary>
public static class ExportsContractMapping
{
    /// <summary>Maps QuestExport explicitly without changing its JSON values.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The mapped value.</returns>
    public static PocketQuests.Contracts.Responses.Exports.QuestExport ToModel(this PocketQuests.Domain.Models.Accounts.QuestExport value) => new(
        value.SchemaVersion,
        value.GeneratedAt,
        value.SnapshotCutoff,
        value.AccountId,
        value.State.ToModel(),
        value.DefinitionRevisions.Select(item => item.ToModel()).ToImmutableArray(),
        value.Completions.Select(item => item.ToModel()).ToImmutableArray(),
        value.Undos.Select(item => item.ToModel()).ToImmutableArray(),
        value.Notice);
}
