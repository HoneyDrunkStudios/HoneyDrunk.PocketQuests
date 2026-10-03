namespace PocketQuests.Data.Entities;

/// <summary>An append-only completion event owned by one account and occurrence.</summary>
public sealed class CompletionEntity
{
    /// <summary>Gets or sets the owning account key, included in all event relationships.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the account-scoped event or operation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the accepted occurrence key within the account.</summary>
    public Guid OccurrenceId { get; set; }

    /// <summary>Gets or sets the authoritative server event time.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Gets or sets the immutable completed revision; null only for pre-migration events.</summary>
    public string? QuestSnapshot { get; set; }
}
