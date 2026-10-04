namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable content/reward version of an account quest definition. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.</summary>
public sealed class QuestDefinitionRevisionEntity
{
    /// <summary>Gets or sets application-generated stable row UUID; never reused.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets definition to which this immutable revision belongs.</summary>
    public Guid QuestDefinitionId { get; set; }

    /// <summary>Gets or sets positive sequence within this definition.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets frozen trimmed quest title, 1 to 120 characters.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets frozen self-attestation criterion, 1 to 2000 characters; may contain personal content.</summary>
    public string Criterion { get; set; } = string.Empty;

    /// <summary>Gets or sets frozen optional quest explanation. Null means none was supplied.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets frozen category of this content revision.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets frozen quest rank F/E/D/C/B/A/S.</summary>
    public string RankCode { get; set; } = string.Empty;

    /// <summary>Gets or sets frozen Small, Medium or Large effort tier.</summary>
    public string EffortCode { get; set; } = string.Empty;

    /// <summary>Gets or sets calibrated nonnegative base XP under the frozen ruleset; never client-authoritative.</summary>
    public long BaseXp { get; set; }

    /// <summary>Gets or sets proposed penalty percentage for new acceptance: 0, 10, 25 or 50. Accepted losses are separately locked.</summary>
    public byte PenaltyPercent { get; set; }

    /// <summary>Gets or sets frozen ruleset version used to derive rewards.</summary>
    public string RulesetVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets schema version of the immutable display-label document.</summary>
    public short DisplaySnapshotVersion { get; set; }

    /// <summary>Gets or sets frozen human-readable names for historical display/export only; typed references and allocations enforce rules. Maximum 65536 UTF-16 bytes.</summary>
    public string DisplaySnapshotJson { get; set; } = string.Empty;

    /// <summary>Gets or sets time the new content revision became effective. UTC instant.</summary>
    public DateTimeOffset EffectiveAt { get; set; }

    /// <summary>Gets or sets receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.</summary>
    public Guid? CommandReceiptId { get; set; }

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
