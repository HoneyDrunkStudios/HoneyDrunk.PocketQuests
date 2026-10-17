using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Quests.Mapping;
using PocketQuests.Services.Quests.Validators;

namespace PocketQuests.Services.Quests;

internal static class QuestTerms
{
    internal static Dictionary<Guid, Quest> Read(QuestTermsRows rows)
    {
        QuestValidator.RequireSupportedTerms(rows);
        var terms = rows.ToTerms();
        QuestValidator.RequireFrozenXp(rows, terms);
        return terms;
    }
}
