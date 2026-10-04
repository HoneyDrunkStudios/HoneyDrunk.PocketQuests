using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Skills;

/// <summary>Retains CustomSkill ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class CustomSkillService(ICustomSkillDataService data) : ICustomSkillService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<CustomSkillEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<CustomSkillEntity> SaveAsync(Guid accountId, CustomSkillEntity value, CancellationToken cancellationToken = default)
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
            || original.NameNormalizationVersion != current.NameNormalizationVersion
            || original.CreationOrdinal != current.CreationOrdinal
            || original.ClientKey != current.ClientKey)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.NameNormalizationVersion != value.NameNormalizationVersion
            || current.CreationOrdinal != value.CreationOrdinal
            || current.ClientKey != value.ClientKey)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.Name = value.Name;
        current.NormalizedName = value.NormalizedName;
        current.Revision = value.Revision;
        current.ArchivedAt = value.ArchivedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplySkillsAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        var profile = change.Aggregate.Profile;
        foreach (var skill in profile.CustomSkills ?? [])
        {
            var id = Guid.Parse(skill.Id);
            var prior = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(s => s.Id == id);
            await SaveAsync(
                account.Id,
                new CustomSkillEntity
                {
                    Id = id,
                    AccountId = account.Id,
                    Name = skill.Name,
                    ClientKey = skill.Id,
                    CreationOrdinal = profile.CustomSkills!.IndexOf(skill) + 1,
                    NormalizedName = skill.Name.ToUpperInvariant(),
                    NameNormalizationVersion = 1,
                    Revision = skill.Revision,
                    ArchivedAt = skill.Archived ? prior?.ArchivedAt ?? recordedAt : null,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
        }
    }
}
