using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Commands;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Verifies bounded current-row paging and reads that neither initialize nor acquire the command lock.</summary>
/// <param name="fixture">Only a newly deployed disposable SQL database.</param>
public sealed class RelationalReadTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private RelationalQuestCommands Store => new(fixture.Connection);

    /// <summary>Keyset traversal remains bounded and complete at empty, medium and large account sizes.</summary>
    /// <param name="count">Synthetic committed occurrence count.</param>
    /// <returns>Completion after checking every page, owner and unchanged account rowversion.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(5000)]
    public async Task PagesStayBoundedOrderedAndOwnedWithoutWritebacks(int count)
    {
        var owner = Identity();
        var other = Identity();
        await Store.Initialize(owner, "America/New_York", Start);
        await Store.Initialize(other, "Etc/UTC", Start);
        var unrelated = Guid.NewGuid();
        await Store.Execute(other, new(Guid.NewGuid(), QuestActions.Accept, unrelated, QuestId: "PQ-CAT-Q01"), Start);
        if (count > 0)
        {
            await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01", DueDate: "2026-01-02"), Start);
            if (count > 1)
            {
                // Populate only the current-row read fixture, not command history. These synthetic
                // copies are never used for command/replay tests or any real account/database.
                await fixture.Execute($"""
                    WITH numbers AS
                    (SELECT TOP ({count - 1}) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
                    INSERT pocketquests.QuestOccurrence
                      (Id,AccountId,QuestDefinitionId,QuestDefinitionRevisionId,CategoryId,DueOn,DeadlineAt,DeadlineTimeZoneId,
                       StateCode,AcceptedAt,IsIndividuallyFrozen,Revision,CreatedAt,ModifiedAt,OriginatedAt,OriginatedOffsetMinutes,CreationOrdinal)
                    SELECT NEWID(),o.AccountId,o.QuestDefinitionId,o.QuestDefinitionRevisionId,o.CategoryId,o.DueOn,o.DeadlineAt,o.DeadlineTimeZoneId,
                           o.StateCode,o.AcceptedAt,0,1,o.CreatedAt,o.ModifiedAt,o.OriginatedAt,o.OriginatedOffsetMinutes,CONVERT(int,n.n+1)
                    FROM pocketquests.QuestOccurrence o JOIN pocketquests.Account a ON a.Id=o.AccountId CROSS JOIN numbers n
                    WHERE a.IdentityUserId='{owner.Subject}' AND o.CreationOrdinal=1;
                    """);
            }
        }

        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner.Subject);
        var ids = new HashSet<Guid>();
        var cursor = 0;
        var pages = 0;
        var timer = Stopwatch.StartNew();
        do
        {
            var page = await Store.ReadOccurrences(owner, cursor, 100, Start);
            Assert.InRange(page.Items.Length, 0, 100);
            Assert.Equal(before.MutationVersion, page.MutationVersion);
            foreach (var item in page.Items)
            {
                Assert.NotEqual(unrelated, item.Occurrence.Id);
                Assert.True(ids.Add(item.Occurrence.Id), "Paging must never repeat an occurrence.");
                Assert.Equal(TimeSpan.FromHours(-5), item.Occurrence.Deadline!.Value.Offset);
            }

            pages++;
            if (page.NextAfter is not { } next)
                break;
            Assert.True(next > cursor);
            cursor = next;
        }
        while (pages <= (count / 100) + 1);
        timer.Stop();
        Assert.Equal(count, ids.Count);
        Assert.Equal(Math.Max(1, (count + 99) / 100), pages);
        var after = await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id);
        Assert.Equal(before.RowVersion, after.RowVersion);
        var directory = Environment.GetEnvironmentVariable("POCKETQUESTS_SCHEMA_EVIDENCE");
        if (directory is not null)
            await File.WriteAllTextAsync(Path.Combine(directory, $"paging-{count}.json"), JsonSerializer.Serialize(new { count, pages, elapsedMilliseconds = timer.ElapsedMilliseconds, pageSize = 100, accountRowVersionUnchanged = true }));
    }

    /// <summary>Holding only the exclusive command application lock does not block ordinary or paged reads.</summary>
    /// <returns>Completion after simultaneous reads finish and missing-account reads leave no rows.</returns>
    [Fact]
    public async Task ReadsAvoidCommandLockAndNeverInitializeMissingAccounts()
    {
        var owner = Identity();
        await Store.Initialize(owner, "Etc/UTC", Start);
        await Store.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"), Start);
        await using var connection = new SqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand("pocketquests.LockAccount", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@IdentityUserId", SqlDbType.VarChar, 30).Value = owner.Subject;
        await command.ExecuteNonQueryAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var state = await Store.Read(owner, Start, timeout.Token);
        var page = await Store.ReadOccurrences(owner, 0, 10, Start, timeout.Token);
        Assert.Single(state.Occurrences);
        Assert.Single(page.Items);
        await transaction.RollbackAsync();
        var missing = Identity();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Store.Read(missing, Start));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Store.ReadOccurrences(missing, 0, 10, Start));
        await using var db = fixture.Context();
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == missing.Subject));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Store.ReadOccurrences(owner, 0, 101, Start));
    }

    private static AccountIdentity Identity() => new("verified-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
}
