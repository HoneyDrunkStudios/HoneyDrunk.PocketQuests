using Microsoft.Data.SqlClient;
using PocketQuests.Application.Exports;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Relational.Commands;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;

namespace PocketQuests.Data.Relational;

/// <summary>Preserves existing request/response contracts while selecting the controlled relational persistence boundary.</summary>
/// <param name="commands">Controlled SQL commands and read-only projections.</param>
public sealed class RelationalQuestStore(RelationalQuestCommands commands) : IQuestStore, ISyncAnchors
{
    /// <inheritdoc />
    public Task<QuestState> Initialize(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken) => Translate(async () =>
    {
        await commands.Initialize(identity, initialZone, now, cancellationToken);
        return await commands.Read(identity, now, cancellationToken);
    });

    /// <inheritdoc />
    public Task<QuestState> Read(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken cancellationToken) => Translate(() => commands.Read(identity, now, cancellationToken));

    /// <inheritdoc />
    public Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken cancellationToken) => Translate(() => commands.Execute(identity, command, now, cancellationToken));

    /// <inheritdoc />
    public Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token) => Translate(() => commands.CreateAnchor(identity, deviceId, bootId, deviceUtc, now, token));

    /// <inheritdoc />
    public Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken cancellationToken) => Translate(() => commands.Export(identity, now, cancellationToken));

    private static async Task<T> Translate<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqlException error) when (error.Number == 51103)
        {
            throw new UnauthorizedAccessException("Account lifecycle no longer permits product access.", error);
        }
        catch (SqlException error) when (error.Number is 51104)
        {
            throw new KeyNotFoundException("Initialize a profile before using its quest state.", error);
        }
        catch (SqlException error) when (error.Number is 51107 or 51108)
        {
            throw new InvalidOperationException("The command conflicts with committed account state.", error);
        }
    }
}
