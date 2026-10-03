using NodaTime.Text;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Schedules;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Definition editing, onboarding, planning and reward selection rules.</summary>
public sealed partial class QuestAggregate
{
    /// <summary>Gets editable account-owned definitions.</summary>
    public List<QuestDefinition> Definitions { get; } = definitions?.ToList() ?? [];

    /// <summary>Gets durable preferences and experience assessments.</summary>
    public PlayerProfile Profile { get; private set; } = profile ?? PlayerProfile.Empty;

    private static void ValidatePlannedTime(string? date, string? time)
    {
        Progression.Require(
            time is null || (date is not null && LocalTimePattern.CreateWithInvariantCulture("HH:mm").Parse(time).Success),
            "Planned time: use HH:mm with a due date. It does not change the end-of-day deadline.");
    }

    private Quest CompletionQuest(Completion completion) => completion.Snapshot
        ?? Occurrences.Single(o => o.Id == completion.OccurrenceId).Quest;

    private void RequireEligible(Quest quest, DateTimeOffset now)
    {
        var state = Project(now);
        Progression.Require(Progression.Eligible(quest, state.Categories.ToDictionary(x => x.Id, x => x.Xp), state.Skills.ToDictionary(x => x.Id, x => x.Xp)), "Rank: every selected skill (or the category when no skills are selected) must meet the rank prerequisite.");
    }

    private void SaveDefinition(QuestCommand command, DateTimeOffset now)
    {
        var input = command.Definition ?? throw new ArgumentException("Definition is required.");
        Progression.Require(Guid.TryParseExact(input.Id, "D", out _), "Definition ID: use a stable UUID.");
        Progression.Require(input.IsCustom, "Only custom definitions can be edited.");
        Progression.Require(input.Title?.Trim().Length is >= 1 and <= QuestRules.MaximumTitleLength, "Title: enter 1–120 characters.");
        Progression.Require(input.Criterion?.Trim().Length is >= 1 and <= QuestRules.MaximumCriterionLength, "Completion criterion: enter 1–2,000 characters describing the achieved outcome.");
        Progression.Require(input.Description is null || input.Description.Length <= QuestRules.MaximumDescriptionLength, "Description: use at most 2,000 characters.");
        Progression.Require(Catalog.Categories.Any(c => c.Id == input.CategoryId), "Category: choose one of the ten categories.");
        Progression.Require(!input.Attributes.IsDefault && !input.Skills.IsDefault, "Allocations: supply both pools, even when empty.");
        Progression.Require(input.Attributes.All(a => a is not null && Catalog.Attributes.Any(c => c.Id == a.Id)), "Attributes: choose known attributes.");
        Progression.Require(input.Skills.All(a => a is not null && SkillItems().Any(c => c.Id == a.Id) && !IsArchivedSkill(a.Id)), "Skills: choose known skills.");
        Progression.Require(input.PenaltyPercent is 0 or 10 or 25 or 50, "Penalty: choose none, 10%, 25% or 50%.");
        _ = Progression.Allocate(input.BaseXp, input.Attributes);
        _ = Progression.Allocate(input.BaseXp, input.Skills);
        var quest = input with { Title = input.Title!.Trim(), Criterion = input.Criterion!.Trim(), Description = input.Description?.Trim() };
        RequireEligible(quest, now);
        var index = Definitions.FindIndex(d => d.Quest.Id == quest.Id);
        var revision = index < 0 ? 0 : Definitions[index].Revision;
        if (command.ExpectedRevision != revision)
            throw new InvalidOperationException("This definition changed. Reopen it before saving.");
        Progression.Require(index < 0 || !Definitions[index].Archived, "Archived definitions cannot be edited.");
        var affected = Occurrences.Where(o => o.Quest.Id == quest.Id && Surviving(o.Id) is null && (o.Lifecycle?.FrozenAt is not null || o.Deadline is null || now < o.Deadline) && o.Lifecycle?.AbandonedAt is null).ToArray();
        foreach (var occurrence in affected)
        {
            Progression.Require(occurrence.Lifecycle?.LockedLoss is null || quest.CategoryId == occurrence.Quest.CategoryId, "Accepted penalty category cannot change.");
            Progression.Require(quest.CategoryId == occurrence.Quest.CategoryId || (occurrence.Lifecycle?.FrozenAt is null && !IsPaused(quest.CategoryId)), "Resume the affected categories before changing a frozen commitment's category.");
            Progression.Require(occurrence.ParentId is null || quest.Effort != Effort.Large, "A linked step must remain Small or Medium.");
            Progression.Require(!Occurrences.Any(o => o.ParentId == occurrence.Id) || quest.Effort == Effort.Large, "A parent with linked steps must remain Large.");
        }

        var definition = new QuestDefinition(quest, revision + 1);
        if (index < 0)
            Definitions.Add(definition);
        else
            Definitions[index] = definition;
        foreach (var occurrence in affected)
            Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { Quest = quest };
        Schedule = Schedule with { Series = [.. Schedule.Series.Select(s => s.Quest.Id == quest.Id && !s.Stopped ? s with { Quest = quest, AutoAcceptPenalty = false } : s)] };
    }

    private void ArchiveDefinition(QuestCommand command, DateTimeOffset now)
    {
        var index = Definitions.FindIndex(d => d.Quest.Id == command.QuestId);
        if (index < 0)
            throw new KeyNotFoundException("Custom definition not found.");
        if (command.ExpectedRevision != Definitions[index].Revision)
            throw new InvalidOperationException("Definition changed.");
        Definitions[index] = Definitions[index] with { Archived = true, Revision = Definitions[index].Revision + 1 };
        foreach (var series in Schedule.Series.Where(s => s.Quest.Id == command.QuestId && !s.Stopped).ToArray())
            StopSeries(command with { SeriesId = series.Id }, now);
    }

    private void AssessSkill(QuestCommand command, DateTimeOffset now)
    {
        Progression.Require(SkillItems().Any(s => s.Id == command.SkillId) && !IsArchivedSkill(command.SkillId!), "Skill: choose a known skill.");
        Progression.Require(command.Experience is not null, "Experience: choose new, practiced, experienced or expert.");
        _ = Progression.Seed(command.Experience!.Value);
        var history = Profile.AssessmentHistory ?? [];
        if (history.All(s => s.SkillId != command.SkillId) && Profile.Assessments.TryGetValue(command.SkillId!, out var previous))
            history = history.Add(new(command.SkillId!, previous, DateTimeOffset.MinValue));
        Profile = Profile with { Assessments = Profile.Assessments.SetItem(command.SkillId!, command.Experience.Value),
            AssessmentHistory = history.Add(new(command.SkillId!, command.Experience.Value, now)) };
    }

    private void SetInterests(QuestCommand command)
    {
        var interests = command.Interests ?? throw new ArgumentException("Interests are required; an empty list is allowed.");
        Progression.Require(
            interests.Length <= Catalog.Categories.Length && interests.Distinct(StringComparer.Ordinal).Count() == interests.Length
            && interests.All(id => Catalog.Categories.Any(c => c.Id == id)),
            "Interests: choose distinct core categories.");
        Profile = Profile with { Interests = [.. interests] };
    }

    private void Plan(QuestCommand command, DateTimeOffset now)
    {
        var occurrence = Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
        Progression.Require(Surviving(occurrence.Id) is null && occurrence.Lifecycle?.FrozenAt is null && occurrence.Lifecycle?.AbandonedAt is null && occurrence.Lifecycle?.SeriesId is null && occurrence.Lifecycle?.LockedLoss is null && (occurrence.Deadline is null || now < occurrence.Deadline), "Only active occurrences can be planned.");
        ValidatePlannedTime(command.DueDate, command.PlannedTime);
        DateTimeOffset? deadline = null;
        if (command.DueDate is not null)
        {
            var date = Scheduling.ParseDate(command.DueDate);
            Progression.Require(date >= Scheduling.LocalDay(now, Zone) && date.Year < 9999, "Due date: choose today or a future date before year 9999.");
            deadline = Scheduling.Deadline(date, Zone);
        }

        Occurrences[Occurrences.IndexOf(occurrence)] = occurrence with { DueDate = command.DueDate, Deadline = deadline, PlannedTime = command.PlannedTime, Lifecycle = (occurrence.Lifecycle ?? new()) with { DeadlineZone = Zone } };
    }

    private void Link(QuestCommand command)
    {
        var step = Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException();
        var parent = Occurrences.SingleOrDefault(o => o.Id == command.ParentId) ?? throw new KeyNotFoundException();
        Progression.Require((step.Quest.Effort is Effort.Small or Effort.Medium) && parent.Quest.Effort == Effort.Large, "Link a Small or Medium step to a Large goal.");
        Progression.Require(step.ParentId is null || step.ParentId == parent.Id, "A step can have only one parent.");
        Occurrences[Occurrences.IndexOf(step)] = step with { ParentId = parent.Id };
    }

    private void SelectReward(QuestCommand command, string kind, DateTimeOffset now)
    {
        Progression.Require(command.RewardId is null || Project(now).Entitlements.Any(e => e.Id == command.RewardId && e.Kind == kind && e.Earned), "Choose an earned reward, or the default.");
        Profile = kind == "Badge" ? Profile with { BadgeId = command.RewardId } : Profile with { FrameId = command.RewardId };
    }
}
