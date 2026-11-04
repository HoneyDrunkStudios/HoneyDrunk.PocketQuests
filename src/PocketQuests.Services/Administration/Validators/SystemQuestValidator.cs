using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Services.Commands.Mapping;

namespace PocketQuests.Services.Administration.Validators;

internal static class SystemQuestValidator
{
    internal const int MaximumTemplates = 200;
    internal const int MaximumRevisions = 1000;

    internal static string Code(Guid id) => "PQ-ADM-" + id.ToString("N");

    internal static Quest Normalize(SaveSystemQuestRequest request)
    {
        if (request.Id == Guid.Empty || request.Quest.IsCustom || request.Quest.Id != Code(request.Id))
            throw new QuestValidationException("Use a stable studio quest identity and system-quest terms.");
        var quest = request.Quest.ToModel();
        QuestRules.ValidateTerms(quest, Catalog.Skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal));
        return quest with { Title = quest.Title.Trim(), Criterion = quest.Criterion.Trim(), Description = quest.Description?.Trim() };
    }

    internal static void RequireSnapshotSize(string snapshot)
    {
        if (System.Text.Encoding.Unicode.GetByteCount(snapshot) > 32768)
            throw new QuestValidationException("The escaped quest content is too large. Shorten the description or completion criterion.");
    }

    internal static void RequireVersion(string? expected, byte[] current)
    {
        if (expected != Convert.ToBase64String(current))
            throw new QuestConflictException("This system quest changed. Reload its saved version before trying again; keep your unsaved text separately.");
    }
}
