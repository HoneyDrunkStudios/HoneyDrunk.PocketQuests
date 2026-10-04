using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Schedules;
using System.Data;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>
/// Relational account commands, typed source history, compact replay and bounded reconciliation.
/// Selected together with its lifecycle adapter by explicit Persistence:Mode=Relational configuration.
/// Identity arguments must come from the existing validated Identity resolver, never the request body.
/// </summary>
/// <param name="connectionString">Connection for the internal command service role.</param>
public sealed partial class RelationalQuestCommands(string connectionString)
{
    private static readonly System.Buffers.SearchValues<char> CanonicalUserCharacters = System.Buffers.SearchValues.Create("0123456789ABCDEFGHJKMNPQRSTVWXYZ");

    /// <summary>Explicitly initializes an account; ordinary reads never create or rewrite it.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="zone">Validated IANA calendar zone.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Completion of the atomic initialization.</returns>
    public async Task Initialize(AccountIdentity identity, string zone, DateTimeOffset now, CancellationToken token = default)
    {
        var canonicalZone = Scheduling.Zone(zone).Id;
        await using var session = await Open(identity, token);
        await using var sql = session.Procedure("pocketquests.InitializeAccount");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@TimeZoneId", SqlDbType.VarChar, canonicalZone, 100);
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now.ToUniversalTime());
        await sql.ExecuteNonQueryAsync(token);
        await session.Transaction.CommitAsync(token);
    }

    /// <summary>Projects source history without writing account rows or materialized balances.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Current source-derived state.</returns>
    public async Task<QuestState> Read(AccountIdentity identity, DateTimeOffset now, CancellationToken token = default)
    {
        await using var session = await Open(identity, token, readOnly: true);
        var account = await Account(session, identity, token);
        var aggregate = await Load(session, account, account.MutationVersion, token);
        var projectionAt = Max(now, account.LastRecordedAt);
        if (aggregate.Reconcile(projectionAt, RequestReconciliationLimit).HasMore)
            throw new ReconciliationPendingException("Recurring deliveries are being reconciled. Retry after maintenance advances the retained backlog.");
        var result = aggregate.Project(projectionAt);
        await session.Transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Executes a supported command or reconstructs its exact original response from compact receipt/history.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="command">Immutable client command.</param>
    /// <param name="now">Server receipt clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The original command state, including its original completion feedback.</returns>
    public async Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token = default)
    {
        var digest = CommandDigest.Compute(command);
        now = now.ToUniversalTime();
        await using var session = await Open(identity, token);
        var account = await Account(session, identity, token);
        var receipt = await session.Context.Set<CommandReceiptEntity>().SingleOrDefaultAsync(r => r.AccountId == account.Id && r.Id == command.OperationId, token);
        if (receipt is not null)
        {
            if (receipt.ApiVersion != 1 || receipt.DigestVersion != 1 || receipt.OutcomeVersion is not (1 or 2))
                throw new NotSupportedException("The stored receipt version requires its original replay implementation.");
            if (!receipt.PayloadDigest.AsSpan().SequenceEqual(digest) || receipt.CommandType != command.Action)
                throw new InvalidOperationException("Operation ID already belongs to a different payload.");
            var original = JsonSerializer.Deserialize<CompactOutcome>(receipt.OutcomeJson) ?? throw new InvalidOperationException("Receipt is invalid.");
            var originalReplay = await ReplayHistory(session, account, receipt.AppliedMutationVersion, token);
            var replay = originalReplay.Aggregate.Project(original.ProjectionAt) with { CompletionOutcome = receipt.OutcomeVersion == 1 ? original.CompletionOutcome : originalReplay.Outcome };
            await session.Transaction.CommitAsync(token);
            return replay;
        }

        var aggregate = await Load(session, account, account.MutationVersion, token);
        var logicalNow = Max(now, account.LastRecordedAt);
        var reconciliation = aggregate.Reconcile(logicalNow, RequestReconciliationLimit);
        if (reconciliation.HasMore)
        {
            await PersistReconciliation(session, identity, account, aggregate, logicalNow, now, RequestReconciliationLimit, hasMore: true, token);
            await session.Transaction.CommitAsync(token);
            throw new ReconciliationPendingException("A bounded recurrence batch committed. Retry this exact command; its proof and receipt were not consumed.");
        }

        var recorded = await RecordedAt(session, account, command, aggregate, now, logicalNow, token);
        var projectionAt = Max(logicalNow, recorded);
        var before = aggregate.Project(projectionAt);
        var actionBudget = RequestReconciliationLimit - reconciliation.Processed;
        var actionReconciliation = aggregate.Apply(command, recorded, actionBudget);
        var result = aggregate.Project(projectionAt);
        if (command.Action == QuestActions.Complete)
            result = result with { CompletionOutcome = CompletionOutcome.Between(before, result, command.OperationId, command.OccurrenceId!.Value) };
        var hasPending = actionReconciliation.HasMore || aggregate.Reconcile(projectionAt, 0).HasMore;
        await CommitMutation(session, identity, account, command, digest, aggregate, result, recorded, logicalNow, projectionAt, now, token, RequestReconciliationLimit, actionBudget, hasPending: hasPending);
        await session.Transaction.CommitAsync(token);
        return result;
    }

    /// <summary>Issues a durable fixed-floor, version-based proof with no age cutoff.</summary>
    /// <param name="identity">Verified canonical Identity.</param>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="bootId">Existing public process/clock identifier.</param>
    /// <param name="deviceUtc">Device wall time at issuance.</param>
    /// <param name="now">Server clock.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The existing public anchor contract.</returns>
    public async Task<SyncAnchor> CreateAnchor(AccountIdentity identity, Guid deviceId, Guid bootId, DateTimeOffset deviceUtc, DateTimeOffset now, CancellationToken token = default)
    {
        if (deviceId == Guid.Empty || bootId == Guid.Empty)
            throw new ArgumentException("Device and process identifiers are required.");
        await using var session = await Open(identity, token);
        var account = await Account(session, identity, token);
        var floor = Max(now, account.LastRecordedAt);
        if (floor > now.AddSeconds(5))
            throw new SyncClockNotReadyException("Server time is behind committed account history.");
        var aggregate = await Load(session, account, account.MutationVersion, token);
        var reconciliation = aggregate.Reconcile(floor, RequestReconciliationLimit);
        if (reconciliation.Processed > 0 || account.HasPendingReconciliation)
        {
            await PersistReconciliation(session, identity, account, aggregate, floor, now.ToUniversalTime(), RequestReconciliationLimit, reconciliation.HasMore, token);
            if (reconciliation.HasMore)
            {
                await session.Transaction.CommitAsync(token);
                throw new ReconciliationPendingException("Recurring deliveries must finish their bounded reconciliation before issuing the next anchor.");
            }
        }

        var id = Guid.NewGuid();
        await using var sql = session.Procedure("pocketquests.IssueSyncAnchor");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@Id", SqlDbType.UniqueIdentifier, id);
        Add(sql, "@DeviceId", SqlDbType.UniqueIdentifier, deviceId);
        Add(sql, "@BootId", SqlDbType.UniqueIdentifier, bootId);
        Add(sql, "@DeviceAt", SqlDbType.DateTimeOffset, deviceUtc.ToUniversalTime());
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now.ToUniversalTime());
        await sql.ExecuteNonQueryAsync(token);
        await session.Transaction.CommitAsync(token);
        return new(id, deviceId, bootId, now.ToUniversalTime(), deviceUtc, floor);
    }

    private static async Task<DateTimeOffset> RecordedAt(Session session, AccountEntity account, QuestCommand command, QuestAggregate aggregate, DateTimeOffset receivedAt, DateTimeOffset logicalNow, CancellationToken token)
    {
        if (command.RecordedTime is not { } proof)
        {
            if (logicalNow > receivedAt.AddSeconds(5))
                throw new SyncClockNotReadyException("Server time is behind committed account history.");
            return logicalNow;
        }

        var anchor = await session.Context.Set<SyncAnchorEntity>().SingleOrDefaultAsync(a => a.AccountId == account.Id && a.Id == proof.AnchorId, token)
            ?? throw new ArgumentException("Offline time anchor belongs to another account or is unavailable.");
        if (anchor.InvalidatedAt is not null || proof.BootId != anchor.BootId || proof.Ordinal <= anchor.LastOrdinal
            || !double.IsFinite(proof.ElapsedMilliseconds) || proof.ElapsedMilliseconds < anchor.LastElapsedMilliseconds || proof.ElapsedMilliseconds < 0)
            throw new ArgumentException("Offline clock ordering cannot be verified. Preserve the action for reconciliation.");
        DateTimeOffset recorded;
        try
        {
            recorded = anchor.ServerAt.AddMilliseconds(proof.ElapsedMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException("Offline elapsed time is outside the supported range.");
        }

        if (Math.Abs((proof.DeviceUtc - anchor.DeviceAt).TotalMilliseconds - proof.ElapsedMilliseconds) > TimeSpan.FromMinutes(2).TotalMilliseconds)
            throw new ArgumentException("The device wall clock changed relative to elapsed time.");

        // Same retry/permanent distinction as reviewed SYNC02 head 7de99e6. No wall-clock expiry.
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The recorded action is ahead of server time; retry the same payload.");
        recorded = Max(recorded, anchor.RecordedTimeFloorAt);
        if (recorded > receivedAt.AddSeconds(5))
            throw new SyncClockNotReadyException("The fixed anchor floor is ahead of server time.");
        if (command.Action == QuestActions.Complete)
        {
            var occurrence = aggregate.Occurrences.SingleOrDefault(o => o.Id == command.OccurrenceId) ?? throw new KeyNotFoundException("Occurrence was not found.");
            var anchored = await Load(session, account, anchor.IssuedMutationVersion, token);
            anchored.Reconcile(Max(anchor.ServerAt, anchor.RecordedTimeFloorAt));
            var snapshot = anchored.Occurrences.SingleOrDefault(o => o.Id == occurrence.Id);
            if (snapshot is null && occurrence.Lifecycle?.SourceAnchorId != anchor.Id)
                throw new ArgumentException("Occurrence was not available in the anchored session.");
            if (occurrence.Lifecycle?.FrozenAt is not null || occurrence.Lifecycle?.AbandonedAt is not null)
                throw new ArgumentException("Occurrence was paused or abandoned; reconcile the recorded action.");
            if (snapshot is not null && !aggregate.Completions.Any(c => c.OccurrenceId == occurrence.Id && aggregate.Undos.All(u => u.CompletionId != c.Id)))
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(occurrence)] = snapshot with { ParentId = occurrence.ParentId };
        }

        return recorded;
    }

    private static async Task<AccountEntity> Account(Session session, AccountIdentity identity, CancellationToken token) =>
        await session.Context.Set<AccountEntity>().SingleOrDefaultAsync(a => a.IdentityUserId == identity.Subject, token)
        ?? throw new KeyNotFoundException("Initialize a profile before using its quest state.");

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => (a > b ? a : b).ToUniversalTime();

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = command.Parameters.Add(name, type);
        if (size != 0)
            parameter.Size = size;
        parameter.Value = value ?? DBNull.Value;
    }

    private static void ValidateIdentity(AccountIdentity identity)
    {
        if (identity.Subject.Length != 30 || !identity.Subject.StartsWith("usr_", StringComparison.Ordinal)
            || identity.Subject.AsSpan(4).ContainsAnyExcept(CanonicalUserCharacters))
            throw new ArgumentException("The trusted resolver must supply a canonical Identity usr_ identifier.");
    }

    private async Task<Session> Open(AccountIdentity identity, CancellationToken token, bool readOnly = false, bool lifecycle = false)
    {
        ValidateIdentity(identity);
        var session = await OpenSession(token, readOnly);
        try
        {
            await using var sql = session.Procedure(lifecycle ? "pocketquests.AcquireAccountLock" : readOnly ? "pocketquests.CheckAccountAccess" : "pocketquests.LockAccount");
            Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
            await sql.ExecuteNonQueryAsync(token);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private async Task<Session> OpenSession(CancellationToken token, bool readOnly = false)
    {
        var session = new Session(connectionString);
        try
        {
            await session.Connection.OpenAsync(token);
            session.Transaction = (SqlTransaction)await session.Connection.BeginTransactionAsync(readOnly ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted, token);
            session.Context = new(new DbContextOptionsBuilder<RelationalQuestReadContext>().UseSqlServer(session.Connection).Options);
            await session.Context.Database.UseTransactionAsync(session.Transaction, token);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private sealed record CompactOutcome(DateTimeOffset ProjectionAt, CompletionOutcome? CompletionOutcome);

    private sealed class Session(string connection) : IAsyncDisposable
    {
        internal SqlConnection Connection { get; } = new(connection);

        internal SqlTransaction Transaction { get; set; } = null!;

        internal RelationalQuestReadContext Context { get; set; } = null!;

        public async ValueTask DisposeAsync()
        {
            if (Context is not null)
                await Context.DisposeAsync();
            if (Transaction is not null)
                await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }

        internal SqlCommand Procedure(string name)
        {
            var command = name switch
            {
                "pocketquests.LockAccount" => new SqlCommand("pocketquests.LockAccount", Connection, Transaction),
                "pocketquests.CheckAccountAccess" => new SqlCommand("pocketquests.CheckAccountAccess", Connection, Transaction),
                "pocketquests.InitializeAccount" => new SqlCommand("pocketquests.InitializeAccount", Connection, Transaction),
                "pocketquests.IssueSyncAnchor" => new SqlCommand("pocketquests.IssueSyncAnchor", Connection, Transaction),
                "pocketquests.CommitQuestCommand" => new SqlCommand("pocketquests.CommitQuestCommand", Connection, Transaction),
                "pocketquests.CommitAccountMutation" => new SqlCommand("pocketquests.CommitAccountMutation", Connection, Transaction),
                "pocketquests.AcquireAccountLock" => new SqlCommand("pocketquests.AcquireAccountLock", Connection, Transaction),
                "pocketquests.SetLifecycleState" => new SqlCommand("pocketquests.SetLifecycleState", Connection, Transaction),
                "pocketquests.StageLifecycleAcknowledgment" => new SqlCommand("pocketquests.StageLifecycleAcknowledgment", Connection, Transaction),
                "pocketquests.PurgeAccount" => new SqlCommand("pocketquests.PurgeAccount", Connection, Transaction),
                "pocketquests.PruneLifecycle" => new SqlCommand("pocketquests.PruneLifecycle", Connection, Transaction),
                _ => throw new ArgumentOutOfRangeException(nameof(name)),
            };
            command.CommandType = CommandType.StoredProcedure;
            return command;
        }
    }
}
