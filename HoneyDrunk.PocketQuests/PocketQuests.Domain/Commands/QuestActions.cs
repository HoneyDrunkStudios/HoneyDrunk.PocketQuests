namespace PocketQuests.Domain.Commands;

/// <summary>Stable action names in the quest command protocol.</summary>
public static class QuestActions
{
    /// <summary>The accept command.</summary>
    public const string Accept = "accept";

    /// <summary>The complete command.</summary>
    public const string Complete = "complete";

    /// <summary>The undo command.</summary>
    public const string Undo = "undo";

    /// <summary>The save-definition command.</summary>
    public const string SaveDefinition = "save-definition";

    /// <summary>The archive-definition command.</summary>
    public const string ArchiveDefinition = "archive-definition";

    /// <summary>The assess-skill command.</summary>
    public const string AssessSkill = "assess-skill";

    /// <summary>The save-skill command.</summary>
    public const string SaveSkill = "save-skill";

    /// <summary>The archive-skill command.</summary>
    public const string ArchiveSkill = "archive-skill";

    /// <summary>The interests command.</summary>
    public const string Interests = "interests";

    /// <summary>The finish-onboarding command.</summary>
    public const string FinishOnboarding = "finish-onboarding";

    /// <summary>The plan command.</summary>
    public const string Plan = "plan";

    /// <summary>The zone command.</summary>
    public const string Zone = "zone";

    /// <summary>The expiry-warnings command.</summary>
    public const string ExpiryWarnings = "expiry-warnings";

    /// <summary>The link command.</summary>
    public const string Link = "link";

    /// <summary>The select-badge command.</summary>
    public const string SelectBadge = "select-badge";

    /// <summary>The select-frame command.</summary>
    public const string SelectFrame = "select-frame";

    /// <summary>The save-series command.</summary>
    public const string SaveSeries = "save-series";

    /// <summary>The stop-series command.</summary>
    public const string StopSeries = "stop-series";

    /// <summary>The pause command.</summary>
    public const string Pause = "pause";

    /// <summary>The resume command.</summary>
    public const string Resume = "resume";

    /// <summary>The resume-occurrence command.</summary>
    public const string ResumeOccurrence = "resume-occurrence";

    /// <summary>The abandon command.</summary>
    public const string Abandon = "abandon";

    /// <summary>The accept-offer command.</summary>
    public const string AcceptOffer = "accept-offer";
}
