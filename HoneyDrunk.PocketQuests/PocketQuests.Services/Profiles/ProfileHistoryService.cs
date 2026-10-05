using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Profiles;

internal sealed class ProfileHistoryService(
    IAccountInterestDataService interests, IQuestCommandInterestDataService commandInterests,
    ICustomSkillDataService skills, ISkillAssessmentDataService assessments,
    ITimeZoneChangeDataService zones, IAccountPauseDataService pauses)
{
    internal async Task Record(QuestMutation change, QuestStateRows rows, CancellationToken token)
    {
        List<AccountInterestEntity> newInterests = [];
        List<QuestCommandInterestEntity> newCommandInterests = [];
        List<CustomSkillEntity> newSkills = [];
        List<SkillAssessmentEntity> newAssessments = [];
        List<TimeZoneChangeEntity> newZones = [];
        List<AccountPauseEntity> newPauses = [];
        var command = change.Command;
        var profile = change.Aggregate.Profile;
        if (command.Action == QuestActions.Interests)
        {
            var existing = rows.Interests.ToDictionary(row => row.CategoryId);
            for (var index = 0; index < profile.Interests.Length; index++)
            {
                var category = profile.Interests[index];
                if (!existing.TryGetValue(category, out var current))
                    newInterests.Add(ProfilePersistenceMapping.ToInterest(change, category, index));
                else if (current.Position != index)
                    ProfilePersistenceMapping.ApplyPosition(current, index);
                newCommandInterests.Add(ProfilePersistenceMapping.ToCommandInterest(change, category, index));
            }
        }

        if (command.Action is QuestActions.SaveSkill or QuestActions.ArchiveSkill)
        {
            var skill = profile.CustomSkills!.Single(item => item.Id == command.SkillId);
            var prior = rows.Skills.SingleOrDefault(row => row.Id == Guid.Parse(skill.Id));
            if (prior is null)
            {
                var row = skill.ToEntity(change, profile.CustomSkills!.IndexOf(skill) + 1);
                row.ArchivedAt = skill.Archived ? change.RecordedAt : null;
                newSkills.Add(row);
            }
            else if (prior.Name != skill.Name || prior.Revision != skill.Revision || (prior.ArchivedAt is not null) != skill.Archived)
            {
                skill.ApplyTo(prior);
                prior.ArchivedAt = skill.Archived ? prior.ArchivedAt ?? change.RecordedAt : null;
                prior.ModifiedAt = QuestClock.Max(prior.ModifiedAt, change.Now);
            }
        }

        if (command.Action == QuestActions.AssessSkill)
            newAssessments.Add(ProfilePersistenceMapping.ToAssessment(change, Progression.Seed(command.Experience!.Value)));
        if (command.Action == QuestActions.Zone && change.Aggregate.Zone != change.TimeZoneBefore)
            newZones.Add(ProfilePersistenceMapping.ToZoneChange(change));

        var existingPauses = rows.Pauses.ToDictionary(row => row.Id);
        for (var index = 0; index < change.Aggregate.Schedule.Pauses.Length; index++)
        {
            var pause = change.Aggregate.Schedule.Pauses[index];
            var id = QuestValues.Derived(change.Account.Id, $"pause/{index}/{pause.CategoryId}/{pause.StartedAt:O}");
            var prior = existingPauses.GetValueOrDefault(id);
            if (prior is null)
            {
                newPauses.Add(pause.ToEntity(change, id, index + 1));
            }
            else if (prior.EndedAt != pause.EndedAt)
            {
                if (prior.EndedAt is not null)
                    throw new InvalidOperationException("A completed pause interval cannot be reopened or rewritten.");
                pause.ApplyTo(prior);
                prior.ModifiedAt = QuestClock.Max(prior.ModifiedAt, change.Now);
            }
        }

        if (command.Action == QuestActions.Interests)
            interests.RemoveRange(rows.Interests.Where(row => !profile.Interests.Contains(row.CategoryId)));
        foreach (var row in newInterests)
            row.CreatedAt = change.Now;
        foreach (var row in newCommandInterests)
            row.CreatedAt = change.Now;
        foreach (var row in newSkills)
            row.CreatedAt = row.ModifiedAt = change.Now;
        foreach (var row in newAssessments)
            row.CreatedAt = change.Now;
        foreach (var row in newZones)
            row.CreatedAt = change.Now;
        foreach (var row in newPauses)
            row.CreatedAt = row.ModifiedAt = change.Now;

        await interests.AddRangeAsync(newInterests, token);
        await commandInterests.AddRangeAsync(newCommandInterests, token);
        await skills.AddRangeAsync(newSkills, token);
        await assessments.AddRangeAsync(newAssessments, token);
        await zones.AddRangeAsync(newZones, token);
        await pauses.AddRangeAsync(newPauses, token);
    }
}
