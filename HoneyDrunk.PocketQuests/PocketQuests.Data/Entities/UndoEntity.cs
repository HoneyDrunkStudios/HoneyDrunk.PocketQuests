namespace PocketQuests.Data.Entities;

/// <summary>An append-only reversal of one account-owned completion.</summary>
public sealed class UndoEntity
{
    /// <summary>Gets or sets the owning account key, included in all event relationships.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the account-scoped event or operation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the specific completion being reversed.</summary>
    public Guid CompletionId { get; set; }

    /// <summary>Gets or sets the authoritative server event time.</summary>
    public DateTimeOffset RecordedAt { get; set; }
}
