using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Services.Profiles.Mapping;
using PocketQuests.Services.Quests;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Profiles;

internal static class ProfileChanges
{
    internal static void Apply(QuestMutation change, QuestStateRows rows, QuestChanges changes)
    {
        var command = change.Command;
        var profile = change.Aggregate.Profile;
        if (command.Action == QuestActions.Interests)
        {
            var existing = rows.Interests.ToDictionary(row => row.CategoryId);
            for (var index = 0; index < profile.Interests.Length; index++)
            {
                var category = profile.Interests[index];
                if (!existing.TryGetValue(category, out var current))
                    changes.Interests.Add(ProfilePersistenceMapping.ToInterest(change, category, index));
                else if (current.Position != index)
                    ProfilePersistenceMapping.ApplyPosition(current, index);
                changes.CommandInterests.Add(ProfilePersistenceMapping.ToCommandInterest(change, category, index));
            }

            changes.RemovedInterests.AddRange(rows.Interests.Where(row => !profile.Interests.Contains(row.CategoryId)));
        }

        if (command.Action is QuestActions.SaveSkill or QuestActions.ArchiveSkill)
        {
            var skill = profile.CustomSkills!.Single(item => item.Id == command.SkillId);
            var prior = rows.Skills.SingleOrDefault(row => row.Id == Guid.Parse(skill.Id));
            if (prior is null)
                changes.Skills.Add(skill.ToEntity(change, profile.CustomSkills!.IndexOf(skill) + 1));
            else if (prior.Name != skill.Name || prior.Revision != skill.Revision || (prior.ArchivedAt is not null) != skill.Archived)
                skill.ApplyTo(prior, change);
        }

        if (command.Action == QuestActions.AssessSkill)
            changes.Assessments.Add(ProfilePersistenceMapping.ToAssessment(change, Progression.Seed(command.Experience!.Value)));
        if (command.Action == QuestActions.Zone && change.Aggregate.Zone != change.TimeZoneBefore)
            changes.Zones.Add(ProfilePersistenceMapping.ToZoneChange(change));

        var pauses = rows.Pauses.ToDictionary(row => row.Id);
        for (var index = 0; index < change.Aggregate.Schedule.Pauses.Length; index++)
        {
            var pause = change.Aggregate.Schedule.Pauses[index];
            var id = QuestValues.Derived(change.Account.Id, $"pause/{index}/{pause.CategoryId}/{pause.StartedAt:O}");
            var prior = pauses.GetValueOrDefault(id);
            if (prior is null)
            {
                changes.Pauses.Add(pause.ToEntity(change, id, index + 1));
            }
            else if (prior.EndedAt != pause.EndedAt)
            {
                if (prior.EndedAt is not null)
                    throw new InvalidOperationException("A completed pause interval cannot be reopened or rewritten.");
                pause.ApplyTo(prior, change);
            }
        }
    }
}
