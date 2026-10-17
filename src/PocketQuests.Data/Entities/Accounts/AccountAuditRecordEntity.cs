namespace PocketQuests.Data.Entities.Accounts;

/// <summary>One row is one ownership association between a personal account and its canonical shared audit record. Classification: Restricted. History: event; Retain with the linked personal audit record. Delete association and owned audit record atomically on approved final erasure; no content duplicated.</summary>
public sealed class AccountAuditRecordEntity
{
    /// <summary>Gets or sets pocket Quests account that exclusively owns this row; supplied by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets canonical shared AuditRecord ID; envelope content and shape remain Audit-owned. Uses DATABASE_DEFAULT collation to match the unchanged shared AuditRecords.Id.</summary>
    public string AuditRecordId { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
