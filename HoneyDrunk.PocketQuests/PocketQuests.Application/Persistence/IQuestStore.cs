using PocketQuests.Application.Exports;
using PocketQuests.Application.Identity;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;

namespace PocketQuests.Application.Persistence;

/// <summary>An account-isolated transaction boundary for quest events and idempotency receipts.</summary>
public interface IQuestStore
{
    /// <summary>Exports one consistent account snapshot under the same transaction lock as commands.</summary>
    /// <param name="identity">The verified account identity.</param>
    /// <param name="now">The authoritative snapshot time.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The complete private product snapshot.</returns>
    Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Loads an account projection and atomically creates the initial profile when necessary.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="initialZone">The IANA timezone used only when creating the account.</param>
    /// <param name="now">Authoritative server UTC time.</param>
    /// <param name="cancellationToken">Cancellation for the database operation.</param>
    /// <returns>The current account projection.</returns>
    Task<QuestState> Read(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Atomically applies a command with its receipt under the account lock.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="command">The command to validate and apply.</param>
    /// <param name="now">Authoritative server UTC time.</param>
    /// <param name="cancellationToken">Cancellation for the database operation.</param>
    /// <returns>The committed result or the same-operation receipt.</returns>
    Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken cancellationToken);
}
