namespace PocketQuests.Data.Entities;

/// <summary>An idempotency receipt committed in the same transaction as its quest events.</summary>
public sealed class OperationEntity
{
    /// <summary>Gets or sets the owning account key, included in all event relationships.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the account-scoped event or operation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the canonical command used to detect conflicting operation reuse.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets the exact serialized result returned for retries.</summary>
    public string Result { get; set; } = string.Empty;
}
