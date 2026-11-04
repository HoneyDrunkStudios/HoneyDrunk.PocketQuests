namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one stable system quest identity. Classification: Public except Restricted operator identifiers. History: mutable draft and publish pointers with rowversion; immutable SystemQuestRevision retains authored content and shared Audit records actions. At most 200 operator-authored identities; retain referenced identities and revisions, never reuse codes. Operator identifiers are cleared on their account erasure.</summary>
public sealed class SystemQuestEntity
{
    /// <summary>Gets or sets stable system quest code, for example PQ-CAT-Q01; never reused.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets canonical category of the system template.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets current public system-quest title; accepted terms use immutable account definition revisions.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets reviewed template version; seeds must agree with the canonical catalog.</summary>
    public string CatalogVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets the Latest saved immutable draft revision of this same system quest. Null for source-controlled starter templates or during atomic initial creation.</summary>
    public Guid? DraftSystemQuestRevisionId { get; set; }

    /// <summary>Gets or sets the Published immutable revision of this same system quest. Null means operator-authored quest is not discoverable; source-controlled starters remain in code.</summary>
    public Guid? PublishedSystemQuestRevisionId { get; set; }

    /// <summary>Gets or sets the SQL concurrency token for explicit draft and publishing changes, not a timestamp.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets the Restricted canonical Identity operator usr_ identifier; null for source-controlled templates or after operator account erasure.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the Restricted canonical Identity operator usr_ identifier; null for source-controlled templates or after operator account erasure.</summary>
    public string? ModifiedBy { get; set; }
}
