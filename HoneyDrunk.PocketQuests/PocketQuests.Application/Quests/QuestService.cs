using PocketQuests.Application.Persistence;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Application.Quests;

/// <summary>Applies server time at the application boundary before invoking durable storage.</summary>
public sealed class QuestService(IQuestStore store, TimeProvider clock)
{
    /// <summary>Explicitly initializes a profile using the existing profile route's selected timezone.</summary>
    /// <param name="identity">Verified account identity.</param>
    /// <param name="initialZone">Initial IANA calendar zone.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Initialized profile state.</returns>
    public Task<QuestState> Initialize(AccountIdentity identity, string initialZone, CancellationToken token) => store.Initialize(identity, initialZone, clock.GetUtcNow(), token);

    /// <summary>Reads current progress at the host clock.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="token">Cancellation for the database operation.</param>
    /// <returns>Current authoritative quest state.</returns>
    public Task<QuestState> Read(AccountIdentity identity, CancellationToken token) => store.Read(identity, clock.GetUtcNow(), token);

    /// <summary>Executes one command with authoritative server time.</summary>
    /// <param name="identity">The trusted account identity established by authentication.</param>
    /// <param name="command">The command to validate and apply.</param>
    /// <param name="token">Cancellation for the database operation.</param>
    /// <returns>The committed result or its previously persisted receipt.</returns>
    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, CancellationToken token) => store.Execute(identity, command, clock.GetUtcNow(), token);
}
