namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable, typed successful action input and its replay clocks, never a serialized command or account snapshot. Classification: Restricted. History: immutable. Erase with its account; no age cutoff on replay history.</summary>
public sealed class QuestCommandHistoryEntity
{
    /// <summary>Gets or sets stable transition UUID, equal to the operation UUID for a client command.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets owning personal account; all references use this same ownership boundary.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets receipt for a client command; null for an internal reconciliation or lifecycle transition. Null means not applicable to this transition.</summary>
    public Guid? CommandReceiptId { get; set; }

    /// <summary>Gets or sets committed account sequence used to reconstruct a receipt or an issued sync anchor.</summary>
    public long AccountMutationVersion { get; set; }

    /// <summary>Gets or sets stable command action or the documented internal reconciliation/lifecycle action.</summary>
    public string ActionCode { get; set; } = string.Empty;

    /// <summary>Gets or sets retained domain replay implementation version; never silently replay through a different ruleset.</summary>
    public string RulesetVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets iANA zone immediately before this transition; the first row preserves the initial account zone.</summary>
    public string TimeZoneBefore { get; set; } = string.Empty;

    /// <summary>Gets or sets uTC clock through which due series deliveries were reconciled before applying the action.</summary>
    public DateTimeOffset ReconciledAt { get; set; }

    /// <summary>Gets or sets verified UTC effective action time; may precede receipt by any duration.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Gets or sets original UTC response projection time, independent of later retries.</summary>
    public DateTimeOffset ProjectionAt { get; set; }

    /// <summary>Gets or sets target or server-resolved new occurrence identity. Null means not applicable to this transition.</summary>
    public Guid? QuestOccurrenceId { get; set; }

    /// <summary>Gets or sets completion selected by Undo. Null means not applicable to this transition.</summary>
    public Guid? QuestCompletionId { get; set; }

    /// <summary>Gets or sets immutable quest terms supplied to a definition, acceptance, series or offer command. Null means not applicable to this transition.</summary>
    public Guid? QuestDefinitionRevisionId { get; set; }

    /// <summary>Gets or sets frozen occurrence revision actually used for a delayed completion after a later edit. Null means not applicable to this transition.</summary>
    public Guid? CompletionTermsRevisionId { get; set; }

    /// <summary>Gets or sets optimistic public definition, skill or series revision supplied by the successful command. Null means not applicable to this transition.</summary>
    public int? ExpectedRevision { get; set; }

    /// <summary>Gets or sets system skill selected for assessment. Null means not applicable to this transition.</summary>
    public string? SystemSkillId { get; set; }

    /// <summary>Gets or sets owned skill selected for creation, editing, archival or assessment. Null means not applicable to this transition.</summary>
    public Guid? CustomSkillId { get; set; }

    /// <summary>Gets or sets validated name supplied by a skill save command; this is historical input, not the mutable current name. Null means not applicable to this transition.</summary>
    public string? SkillName { get; set; }

    /// <summary>Gets or sets validated experience selected for an assessment. Null means not applicable to this transition.</summary>
    public string? ExperienceCode { get; set; }

    /// <summary>Gets or sets requested due or first-delivery local date. Null means not applicable to this transition.</summary>
    public DateOnly? DueOn { get; set; }

    /// <summary>Gets or sets requested local hour and minute. Null means not applicable to this transition.</summary>
    public TimeOnly? PlannedTime { get; set; }

    /// <summary>Gets or sets owned parent selected by a link command. Null means not applicable to this transition.</summary>
    public Guid? ParentQuestOccurrenceId { get; set; }

    /// <summary>Gets or sets selected badge or frame; null also records an explicit clear. Null means not applicable to this transition.</summary>
    public string? ProfileRewardId { get; set; }

    /// <summary>Gets or sets owned recurrence selected for creation, edit or stop. Null means not applicable to this transition.</summary>
    public Guid? QuestSeriesId { get; set; }

    /// <summary>Gets or sets requested recurrence unit. Null means not applicable to this transition.</summary>
    public string? CadenceCode { get; set; }

    /// <summary>Gets or sets requested positive recurrence interval. Null means not applicable to this transition.</summary>
    public int? Interval { get; set; }

    /// <summary>Gets or sets category pause/resume target; null means the whole account. Null means not applicable to this transition.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Gets or sets a value indicating whether the successful command explicitly confirmed penalty terms.</summary>
    public bool ConfirmPenalty { get; set; }

    /// <summary>Gets or sets exact penalty amount accepted by the command. Null means not applicable to this transition.</summary>
    public int? AcceptedLoss { get; set; }

    /// <summary>Gets or sets a value indicating whether the client supplied accepted quest terms; reconstruct them from the immutable typed terms reference.</summary>
    public bool HasAcceptedTerms { get; set; }

    /// <summary>Gets or sets requested IANA zone for a zone-change command. Null means not applicable to this transition.</summary>
    public string? NewTimeZone { get; set; }

    /// <summary>Gets or sets optimistic previous IANA zone supplied by the command. Null means not applicable to this transition.</summary>
    public string? ExpectedTimeZone { get; set; }

    /// <summary>Gets or sets a value indicating whether a successful zone change was explicitly confirmed.</summary>
    public bool ConfirmZoneChange { get; set; }

    /// <summary>Gets or sets requested expiry-warning preference. Null means not applicable to this transition.</summary>
    public bool? HasExpiryWarnings { get; set; }

    /// <summary>Gets or sets original proof anchor, retaining offline acceptance provenance without copying an anchor snapshot. Null means not applicable to this transition.</summary>
    public Guid? SourceSyncAnchorId { get; set; }

    /// <summary>Gets or sets uTC server insertion instant; never substituted for the effective action clock.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets maximum due-delivery cursor steps performed before this action; zero records lifecycle transitions that intentionally do not reconcile before freezing. Retain this input for exact partial-batch replay.</summary>
    public int ReconciliationLimit { get; set; }

    /// <summary>Gets or sets maximum due-delivery cursor work performed by the action itself, such as a zone change or series creation; retained independently of the pre-action budget for exact original-response replay.</summary>
    public int ActionReconciliationLimit { get; set; }
}
