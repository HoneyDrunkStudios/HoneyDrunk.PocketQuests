namespace PocketQuests.Api.Contracts.Quests.Occurrences;

/// <summary>Current occurrence status derived from surviving events and deadline.</summary>
public enum QuestStatus
{
    /// <summary>Available for completion before any deadline.</summary>
    Active,

    /// <summary>Has a surviving completion.</summary>
    Completed,

    /// <summary>Has passed its deadline without a surviving completion.</summary>
    Missed,

    /// <summary>Explicitly frozen until resumed, with no deadline miss while frozen.</summary>
    Frozen,

    /// <summary>Explicitly abandoned without a reward.</summary>
    Abandoned,

    /// <summary>Delivered offer that has not been accepted and cannot earn or lose XP.</summary>
    Offered
}
