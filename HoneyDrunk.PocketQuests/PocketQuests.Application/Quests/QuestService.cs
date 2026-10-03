using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;

namespace PocketQuests.Application.Quests;

/// <summary>Applies server time at the application boundary before invoking durable storage.</summary>
public sealed class QuestService(IQuestStore store, TimeProvider clock)
{
    /// <summary>Reads current progress and initializes a first account with its local timezone.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="initialZone">The IANA timezone used only when creating the account.</param>
    /// <param name="token">Cancellation for the database operation.</param>
    /// <returns>Current authoritative quest state.</returns>
    public Task<QuestState> Read(AccountIdentity identity, string initialZone, CancellationToken token) => store.Read(identity, initialZone, clock.GetUtcNow(), token);

    /// <summary>Executes one command with authoritative server time.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="command">The command to validate and apply.</param>
    /// <param name="token">Cancellation for the database operation.</param>
    /// <returns>The committed result or its previously persisted receipt.</returns>
    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, CancellationToken token) => store.Execute(identity, command, clock.GetUtcNow(), token);
}
