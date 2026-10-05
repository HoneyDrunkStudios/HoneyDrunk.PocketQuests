using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Exports.Mapping;

internal static class ExportMapping
{
    internal static QuestExport ToExport(
        this QuestState state,
        DateTimeOffset generatedAt,
        DateTimeOffset cutoff,
        Guid accountId,
        IEnumerable<QuestDefinition> definitions,
        IEnumerable<Completion> completions,
        IEnumerable<UndoEvent> undos) =>
        new(1, generatedAt, cutoff, accountId, state, [.. definitions], [.. completions], [.. undos]);
}
