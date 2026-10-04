using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Application.Persistence;

/// <summary>An account-isolated transaction boundary for quest events and idempotency receipts.</summary>
public interface IQuestStore
{
    /// <summary>Explicitly initializes a profile and returns current state. Ordinary reads require an existing account.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="initialZone">Initial IANA calendar zone.</param>
    /// <param name="now">Authoritative host clock.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The initialized profile's state.</returns>
    Task<QuestState> Initialize(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Exports one consistent account snapshot; relational exports use a read-only transaction.</summary>
    /// <param name="identity">The verified account identity.</param>
    /// <param name="now">The authoritative snapshot time.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The complete private product snapshot.</returns>
    Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Loads an account projection. Relational reads never create or rewrite account state.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="now">Authoritative server UTC time.</param>
    /// <param name="cancellationToken">Cancellation for the database operation.</param>
    /// <returns>The current account projection.</returns>
    Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Atomically applies a command with its receipt under the account lock.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="command">The command to validate and apply.</param>
    /// <param name="now">Authoritative server UTC time.</param>
    /// <param name="cancellationToken">Cancellation for the database operation.</param>
    /// <returns>The committed result or the same-operation receipt.</returns>
    Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken cancellationToken);
}
