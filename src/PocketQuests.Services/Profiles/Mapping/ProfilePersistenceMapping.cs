using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Services.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Profiles.Mapping;

internal static class ProfilePersistenceMapping
{
    internal static AccountInterestEntity ToInterest(QuestMutation change, string category, int position) => new()
    {
        AccountId = change.Account.Id,
        CategoryId = category,
        Position = position,
    };

    internal static void ApplyPosition(AccountInterestEntity target, int position) => target.Position = position;

    internal static QuestCommandInterestEntity ToCommandInterest(QuestMutation change, string category, int position) => new()
    {
        AccountId = change.Account.Id,
        QuestCommandHistoryId = change.Command.OperationId,
        CategoryId = category,
        Position = position,
    };

    internal static CustomSkillEntity ToEntity(this CustomSkill skill, QuestMutation change, int ordinal) => new()
    {
        Id = Guid.Parse(skill.Id),
        AccountId = change.Account.Id,
        Name = skill.Name,
        ClientKey = skill.Id,
        CreationOrdinal = ordinal,
        NormalizedName = skill.Name.ToUpperInvariant(),
        NameNormalizationVersion = 1,
        Revision = skill.Revision,
    };

    internal static void ApplyTo(this CustomSkill skill, CustomSkillEntity target)
    {
        target.Name = skill.Name;
        target.NormalizedName = skill.Name.ToUpperInvariant();
        target.Revision = skill.Revision;
    }

    internal static SkillAssessmentEntity ToAssessment(QuestMutation change, long seed)
    {
        var (system, custom) = QuestValues.Skill(change.Command.SkillId!);
        return new()
        {
            Id = change.Command.OperationId,
            AccountId = change.Account.Id,
            SystemSkillId = system,
            CustomSkillId = custom,
            ExperienceCode = change.Command.Experience!.Value.ToString(),
            SeedXp = seed,
            RulesetVersion = "1.0",
            EffectiveAt = change.RecordedAt,
            CommandReceiptId = change.Command.OperationId,
        };
    }

    internal static TimeZoneChangeEntity ToZoneChange(QuestMutation change) => new()
    {
        Id = change.Command.OperationId,
        AccountId = change.Account.Id,
        FromTimeZoneId = change.TimeZoneBefore,
        ToTimeZoneId = change.Aggregate.Zone,
        EffectiveAt = change.RecordedAt,
        CommandReceiptId = change.Command.OperationId,
    };

    internal static AccountPauseEntity ToEntity(this PauseWindow pause, QuestMutation change, Guid id, int ordinal) => new()
    {
        Id = id,
        AccountId = change.Account.Id,
        ScopeCode = "Category",
        CreationOrdinal = ordinal,
        CategoryId = pause.CategoryId,
        StartedAt = pause.StartedAt,
        EndedAt = pause.EndedAt,
        CommandReceiptId = change.ReceiptId,
    };

    internal static void ApplyTo(this PauseWindow pause, AccountPauseEntity target)
    {
        target.EndedAt = pause.EndedAt;
    }
}
