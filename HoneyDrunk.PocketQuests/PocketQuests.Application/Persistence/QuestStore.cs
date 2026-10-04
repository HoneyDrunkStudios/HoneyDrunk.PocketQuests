using PocketQuests.Application.Synchronization;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Application.Persistence;

/// <summary>Connects application operations to the scoped domain transaction service.</summary>
/// <param name="quests">Domain workflows over ordinary EF persistence.</param>
public sealed class QuestStore(IQuestService quests) : IQuestStore, ISyncAnchors
{
    /// <inheritdoc />
    public async Task<QuestState> Initialize(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await quests.Initialize(identity, initialZone, now, cancellationToken);
        return await quests.Read(identity, now, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken) => quests.Read(identity, now, cancellationToken);

    /// <inheritdoc />
    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken cancellationToken) => quests.Execute(identity, command, now, cancellationToken);

    /// <inheritdoc />
    public Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token) => quests.CreateAnchor(identity, deviceId, bootId, deviceUtc, now, token);

    /// <inheritdoc />
    public Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken) => quests.Export(identity, now, cancellationToken);
}
