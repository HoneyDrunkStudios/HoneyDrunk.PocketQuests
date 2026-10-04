using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Domain.Models.Quests;

/// <summary>Validated command state shared by entity services inside one transaction; never serialized or stored as a snapshot.</summary>
public sealed record QuestCommit(AccountEntity Account, QuestCommand Command, QuestAggregate Aggregate, QuestState State,
    DateTimeOffset RecordedAt, DateTimeOffset ReconciledAt, DateTimeOffset ProjectionAt, DateTimeOffset Now,
    int ReconciliationLimit, int ActionReconciliationLimit, bool IsInternal = false, bool HasPending = false)
{
    /// <summary>Gets the version captured before any tracked Account fields change.</summary>
    public long Version { get; } = checked(Account.MutationVersion + 1);

    /// <summary>Gets the original timezone required to replay this command.</summary>
    public string TimeZoneBefore { get; } = Account.TimeZoneId;

    /// <summary>Gets the client receipt identifier, absent for internal maintenance.</summary>
    public Guid? ReceiptId => IsInternal ? null : Command.OperationId;
}
