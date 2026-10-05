using PocketQuests.Contracts.Requests.Commands;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Commands;
using DomainQuest = PocketQuests.Domain.Models.Quests.Quest;

namespace PocketQuests.Services.Quests.Validators;

/// <summary>Input and retained-source validation for quest operations and historical replay.</summary>
public static class QuestValidator
{
    private const string RulesetVersion = "1.0";
    private const int DisplaySnapshotVersion = 2;
    private const string OperationIdRequired = "An operation ID is required.";
    private const string AllocationsRequired = "Allocation entries are required.";
    private const string UnsupportedTerms = "Historical quest terms require their retained ruleset and display version.";
    private const string UnsupportedFrozenXp = "Frozen XP rules do not match the retained domain implementation.";
    private const string MissingCurrentRevision = "The current definition or series revision is missing from retained history.";
    private const string InvalidSeriesTerms = "A current series must point to the definition in its owned immutable configuration.";
    private const string InvalidSourceEvent = "Completion and Undo source events must retain their original event kind.";
    private const string InvalidPauseScope = "Effective pause history must use category union intervals.";

    /// <summary>Checks completion inputs without reading storage or changing state.</summary>
    /// <param name="request">The supplied command.</param>
    /// <returns>Input messages; an empty list means valid.</returns>
    public static List<string> ValidateCompletion(QuestCommand request)
    {
        var errors = new List<string>();
        if (request.OperationId == Guid.Empty)
            errors.Add(OperationIdRequired);
        return errors;
    }

    /// <summary>Checks nullable allocation entries before pure mapping; other business rules remain in the domain.</summary>
    /// <param name="request">The supplied command.</param>
    /// <returns>Input messages.</returns>
    public static List<string> ValidateInput(QuestCommand request)
    {
        var errors = request.Action == QuestActions.Complete ? ValidateCompletion(request) : [];
        if ((request.Definition is { } definition && (definition.Attributes.Any(item => item is null) || definition.Skills.Any(item => item is null)))
            || (request.AcceptedQuest is { } accepted && (accepted.Attributes.Any(item => item is null) || accepted.Skills.Any(item => item is null))))
            errors.Add(AllocationsRequired);
        return errors;
    }

    internal static void RequireSupportedTerms(QuestTermsRows rows)
    {
        if (rows.DefinitionRevisions.Any(row => row.RulesetVersion != RulesetVersion || row.DisplaySnapshotVersion != DisplaySnapshotVersion))
            throw new NotSupportedException(UnsupportedTerms);
    }

    internal static void RequireFrozenXp(QuestTermsRows rows, IReadOnlyDictionary<Guid, DomainQuest> terms)
    {
        if (rows.DefinitionRevisions.Any(row => terms[row.Id].BaseXp != row.BaseXp))
            throw new NotSupportedException(UnsupportedFrozenXp);
    }

    internal static List<string> ValidateSources(QuestStateRows rows)
    {
        var errors = new List<string>();
        var events = rows.Events.ToDictionary(row => row.Id);
        if (rows.CurrentDefinitions.Any(row => row.Revision is null) || rows.CurrentSeries.Any(row => row.Revision is null))
            errors.Add(MissingCurrentRevision);
        var revisions = rows.DefinitionRevisions.ToDictionary(row => row.Id);
        if (rows.CurrentSeries.Any(row => row.Revision is not null && (!revisions.TryGetValue(row.Revision.QuestDefinitionRevisionId, out var terms) || terms.QuestDefinitionId != row.Head.QuestDefinitionId)))
            errors.Add(InvalidSeriesTerms);
        if (rows.Completions.Any(row => !events.TryGetValue(row.Id, out var completed) || completed.EventCode != "Completed"
            || (row.UndoQuestOccurrenceEventId is { } undo && (!events.TryGetValue(undo, out var undone) || undone.EventCode != "Undone"))))
            errors.Add(InvalidSourceEvent);
        if (rows.Pauses.Any(row => row.ScopeCode != "Category" || row.CategoryId is null))
            errors.Add(InvalidPauseScope);
        return errors;
    }
}
