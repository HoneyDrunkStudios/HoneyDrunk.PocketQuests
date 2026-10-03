namespace PocketQuests.Data.Entities;

/// <summary>Stores the immutable accepted terms and deadline for one quest occurrence.</summary>
public sealed class OccurrenceEntity
{
    /// <summary>Gets or sets the owning account key, included in all event relationships.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the account-scoped event or operation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the accepted reward terms as JSON inside SQL Server.</summary>
    public string QuestSnapshot { get; set; } = string.Empty;

    /// <summary>Gets or sets the inclusive local due date, or null for unscheduled quests.</summary>
    public string? DueDate { get; set; }

    /// <summary>Gets or sets the exclusive absolute deadline captured at acceptance.</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>Gets or sets the server acceptance timestamp.</summary>
    public DateTimeOffset AcceptedAt { get; set; }

    /// <summary>Gets or sets the optional local organizational time.</summary>
    public string? PlannedTime { get; set; }

    /// <summary>Gets or sets the separately attested Large goal in the same account.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets durable delivery, freeze and abandonment state.</summary>
    public string? Lifecycle { get; set; }
}
