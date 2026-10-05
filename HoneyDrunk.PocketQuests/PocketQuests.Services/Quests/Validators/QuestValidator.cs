using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Data.Queries.Quests;
using DomainQuest = PocketQuests.Domain.Models.Quests.Quest;

namespace PocketQuests.Services.Quests.Validators;

/// <summary>Input and retained-source validation for the completion operation.</summary>
public static class QuestValidator
{
    /// <summary>Checks completion inputs without reading storage or changing state.</summary>
    /// <param name="request">The supplied command.</param>
    /// <returns>Input messages; an empty list means valid.</returns>
    public static List<string> ValidateCompletion(QuestCommand request)
    {
        var errors = new List<string>();
        if (request.OperationId == Guid.Empty)
            errors.Add("An operation ID is required.");
        return errors;
    }

    /// <summary>Checks nullable allocation entries before pure mapping; other business rules remain in the domain.</summary>
    /// <param name="request">The supplied command.</param>
    /// <returns>Input messages.</returns>
    public static List<string> ValidateInput(QuestCommand request)
    {
        var errors = request.Action == "complete" ? ValidateCompletion(request) : [];
        if ((request.Definition is { } definition && (definition.Attributes.Any(item => item is null) || definition.Skills.Any(item => item is null)))
            || (request.AcceptedQuest is { } accepted && (accepted.Attributes.Any(item => item is null) || accepted.Skills.Any(item => item is null))))
            errors.Add("Allocation entries are required.");
        return errors;
    }

    internal static List<string> ValidateSources(QuestCompletionRows rows, IReadOnlyDictionary<Guid, DomainQuest> terms)
    {
        var errors = new List<string>();
        var events = rows.Events.ToDictionary(row => row.Id);
        if (rows.CurrentDefinitions.Any(row => row.Revision is null) || rows.CurrentSeries.Any(row => row.Revision is null))
            errors.Add("The current definition or series revision is missing from retained history.");
        if (rows.DefinitionRevisions.Any(row => row.RulesetVersion != "1.0" || row.DisplaySnapshotVersion != 2 || terms[row.Id].BaseXp != row.BaseXp))
            errors.Add("Historical quest terms require their retained ruleset and display version.");
        if (rows.Completions.Any(row => !events.TryGetValue(row.Id, out var completed) || completed.EventCode != "Completed"
            || (row.UndoQuestOccurrenceEventId is { } undo && (!events.TryGetValue(undo, out var undone) || undone.EventCode != "Undone"))))
            errors.Add("Completion and Undo source events must retain their original event kind.");
        if (rows.Pauses.Any(row => row.ScopeCode != "Category" || row.CategoryId is null))
            errors.Add("Effective pause history must use category union intervals.");
        return errors;
    }
}
