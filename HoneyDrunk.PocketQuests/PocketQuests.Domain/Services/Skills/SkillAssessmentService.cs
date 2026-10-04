using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Skills;

/// <summary>Retains SkillAssessment ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class SkillAssessmentService(ISkillAssessmentDataService data) : ISkillAssessmentService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SkillAssessmentEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<SkillAssessmentEntity> SaveAsync(Guid accountId, SkillAssessmentEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.AccountId != current.AccountId
            || original.SystemSkillId != current.SystemSkillId
            || original.CustomSkillId != current.CustomSkillId
            || original.ExperienceCode != current.ExperienceCode
            || original.SeedXp != current.SeedXp
            || original.RulesetVersion != current.RulesetVersion
            || original.EffectiveAt != current.EffectiveAt
            || original.CommandReceiptId != current.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.SystemSkillId != value.SystemSkillId
            || current.CustomSkillId != value.CustomSkillId
            || current.ExperienceCode != value.ExperienceCode
            || current.SeedXp != value.SeedXp
            || current.RulesetVersion != value.RulesetVersion
            || current.EffectiveAt != value.EffectiveAt
            || current.CommandReceiptId != value.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task RecordAssessmentAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        if (command.Action == QuestActions.AssessSkill)
        {
            var (system, custom) = QuestValues.Skill(command.SkillId!);
            await SaveAsync(
                account.Id,
                new SkillAssessmentEntity
                {
                    Id = command.OperationId,
                    AccountId = account.Id,
                    SystemSkillId = system,
                    CustomSkillId = custom,
                    ExperienceCode = command.Experience!.Value.ToString(),
                    SeedXp = Progression.Seed(command.Experience.Value),
                    RulesetVersion = "1.0",
                    EffectiveAt = recordedAt,
                    CommandReceiptId = command.OperationId,
                    CreatedAt = now
                },
                token);
        }
    }
}
