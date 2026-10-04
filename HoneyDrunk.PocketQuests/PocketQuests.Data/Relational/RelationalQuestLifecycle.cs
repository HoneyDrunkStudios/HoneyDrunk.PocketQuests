using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using HoneyDrunk.Identity.Abstractions.Accounts;
using PocketQuests.Data.AccountLifecycle;
using PocketQuests.Data.Relational.Commands;

namespace PocketQuests.Data.Relational;

/// <summary>Adapts the existing private consumer/authentication contract to the reviewed relational writer.</summary>
/// <param name="commands">Controlled relational boundary.</param>
/// <param name="clock">Host clock.</param>
public sealed class RelationalQuestLifecycle(RelationalQuestCommands commands, TimeProvider clock) : IQuestLifecycle
{
    /// <inheritdoc />
    public Task ObserveActive(UserRecord user, CancellationToken token = default) => commands.ObserveActive(user, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Receive(LifecycleIntent intent, string acknowledgmentQueue, CancellationToken token = default) => commands.ReceiveLifecycle(intent, acknowledgmentQueue, clock.GetUtcNow(), token);

    /// <inheritdoc />
    public Task Prune(CancellationToken token = default) => commands.PruneLifecycle(clock.GetUtcNow(), token);
}
