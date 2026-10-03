using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Custom skills retain their identities and XP across names, categories and archival.</summary>
public sealed partial class QuestAggregate
{
    private ImmutableArray<NamedItem> SkillItems() => [.. Catalog.Skills, .. (Profile.CustomSkills ?? []).Select(s => new NamedItem(s.Id, s.Name))];

    private bool IsArchivedSkill(string id) => Profile.CustomSkills?.Any(s => s.Id == id && s.Archived) == true;

    private Experience AssessmentAt(string id, DateTimeOffset at)
    {
        var history = Profile.AssessmentHistory?.Where(s => s.SkillId == id).ToArray() ?? [];
        return history.Length == 0 ? Profile.Assessments.GetValueOrDefault(id)
            : history.Where(s => s.At <= at).OrderBy(s => s.At).LastOrDefault()?.Experience ?? Experience.New;
    }

    private void SaveSkill(QuestCommand command)
    {
        Progression.Require(Guid.TryParseExact(command.SkillId, "D", out _), "Skill ID: use a stable UUID.");
        var name = command.SkillName?.Trim();
        Progression.Require(name?.Length is >= 1 and <= 80, "Skill name: enter 1–80 characters.");
        Progression.Require(SkillItems().All(s => s.Id == command.SkillId || !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)), "Choose a unique skill name, including archived skills.");
        var skills = Profile.CustomSkills ?? [];
        var prior = skills.SingleOrDefault(s => s.Id == command.SkillId);
        if (command.ExpectedRevision != (prior?.Revision ?? 0))
            throw new InvalidOperationException("Skill changed. Reload before saving.");
        Progression.Require(prior?.Archived != true, "Archived skills retain their history and cannot be edited.");
        var skill = new CustomSkill(command.SkillId!, name!, (prior?.Revision ?? 0) + 1);
        Profile = Profile with { CustomSkills = prior is null ? skills.Add(skill) : skills.Replace(prior, skill) };
    }

    private void ArchiveSkill(QuestCommand command)
    {
        var skills = Profile.CustomSkills ?? [];
        var prior = skills.SingleOrDefault(s => s.Id == command.SkillId) ?? throw new KeyNotFoundException("Custom skill not found.");
        if (command.ExpectedRevision != prior.Revision)
            throw new InvalidOperationException("Skill changed. Reload before archiving.");
        Progression.Require(
            !Definitions.Any(d => !d.Archived && d.Quest.Skills.Any(s => s.Id == prior.Id))
            && !Schedule.Series.Any(s => !s.Stopped && s.Quest.Skills.Any(a => a.Id == prior.Id)),
            "Remove this skill from active definitions and stop its series before archiving. Accepted commitments and earned XP are preserved.");
        Profile = Profile with { CustomSkills = skills.Replace(prior, prior with { Archived = true, Revision = prior.Revision + 1 }) };
    }
}
