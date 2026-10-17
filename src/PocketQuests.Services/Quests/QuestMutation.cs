using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Services.Quests;

/// <summary>Validated operation inputs and original account version shared by feature services in one transaction; never serialized or stored as a snapshot.</summary>
internal sealed record QuestMutation(AccountEntity Account, QuestCommand Command, QuestAggregate Aggregate, QuestState State,
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
