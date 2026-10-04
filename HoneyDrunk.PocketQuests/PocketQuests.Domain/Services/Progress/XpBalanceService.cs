using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Retains XpBalance ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class XpBalanceService(IXpBalanceDataService data) : IXpBalanceService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<XpBalanceEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<XpBalanceEntity> SaveAsync(Guid accountId, XpBalanceEntity value, CancellationToken cancellationToken = default)
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
            || original.AccountId != current.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.TrackCode = value.TrackCode;
        current.CategoryId = value.CategoryId;
        current.AttributeId = value.AttributeId;
        current.SystemSkillId = value.SystemSkillId;
        current.CustomSkillId = value.CustomSkillId;
        current.EarnedXp = value.EarnedXp;
        current.SeedXp = value.SeedXp;
        current.Level = value.Level;
        current.ProjectionVersion = value.ProjectionVersion;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var state = change.State;
        var projectionAt = change.ProjectionAt;

        await StageBalanceAsync(change, "Overall", "overall", state.OverallXp, state.OverallLevel, 0, token);
        foreach (var (track, values) in new[] { ("Category", values: state.Categories), ("Attribute", values: state.Attributes), ("Skill", values: state.Skills) })
            foreach (var balance in values)
            {
                var assessment = aggregate.Profile.AssessmentHistory?.Where(a => a.SkillId == balance.Id && a.At <= projectionAt).OrderBy(a => a.At).LastOrDefault();
                var seed = track == "Skill" && assessment is not null ? Progression.Seed(assessment.Experience) : 0;
                await StageBalanceAsync(change, track, balance.Id, balance.Xp - seed, balance.Level, seed, token);
            }

        data.RemoveRange((await data.GetByAccountIdAsync(account.Id, token)).Where(row => row.ProjectionVersion != change.Version));
    }

    private async Task StageBalanceAsync(QuestCommit change, string track, string target, long earned, int level, long seed, CancellationToken token)
    {
        var account = change.Account;
        var now = change.Now;

        var (system, custom) = track == "Skill" ? QuestValues.Skill(target) : default;
        await SaveAsync(
            account.Id,
            new XpBalanceEntity
            {
                Id = QuestValues.Derived(account.Id, $"balance/{track}/{target}"),
                AccountId = account.Id,
                TrackCode = track,
                CategoryId = track == "Category" ? target : null,
                AttributeId = track == "Attribute" ? target : null,
                SystemSkillId = system,
                CustomSkillId = custom,
                EarnedXp = earned,
                SeedXp = seed,
                Level = level,
                ProjectionVersion = change.Version,
                CreatedAt = now,
                ModifiedAt = now
            },
            token);
    }
}
