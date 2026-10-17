using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.SchemaTests;

/// <summary>Real EF rollback and immutable-source negatives replacing the removed custom SQL writer probes.</summary>
/// <param name="fixture">The isolated disposable database.</param>
public sealed class EfBusinessServiceSqlTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Public commands cannot invoke maintenance/lifecycle actions or consume history for rejected inputs.</summary>
    /// <param name="action">An unsupported public action.</param>
    /// <returns>Completion after rollback and a valid same-scope retry.</returns>
    [Theory]
    [InlineData("$reconcile")]
    [InlineData("$lifecycle-pause")]
    [InlineData("")]
    [InlineData(null)]
    public async Task RejectedCommandLeavesNoReceiptHistoryOrAccountWrite(string? action)
    {
        var owner = Identity();
        var workflow = fixture.Commands();
        await workflow.Initialize(owner, "Etc/UTC", Start);
        await using var evidence = fixture.Context();
        var before = await evidence.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
        var operation = Guid.NewGuid();
        await Assert.ThrowsAsync<QuestValidationException>(() => workflow.Execute(owner, new(operation, action!), Start));
        Assert.False(await evidence.CommandReceipt.AnyAsync(row => row.Id == operation));
        Assert.False(await evidence.QuestCommandHistory.AnyAsync(row => row.CommandReceiptId == operation));
        Assert.Equal(before.RowVersion, (await evidence.Account.SingleAsync(row => row.Id == before.Id)).RowVersion);
        var accepted = await workflow.Execute(owner, new(operation, QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"), Start);
        Assert.Single(accepted.Occurrences);
    }

    /// <summary>Tracked aliases cannot rewrite immutable scalar values or mutable byte-array receipt digests.</summary>
    /// <param name="mutateDigest">Whether to mutate the receipt byte array rather than immutable terms.</param>
    /// <returns>Completion after rejecting a real tracked entity and checking persisted history remains unchanged.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrackedHistoryMutationIsRejectedAndDiscarded(bool mutateDigest)
    {
        var owner = Identity();
        var workflow = fixture.Commands();
        await workflow.Initialize(owner, "Etc/UTC", Start);
        var accepted = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01");
        var original = await workflow.Execute(owner, accepted, Start);
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
            if (mutateDigest)
            {
                var receipt = await db.CommandReceipt.SingleAsync(row => row.Id == accepted.OperationId);
                receipt.PayloadDigest[0] ^= 0xff;
                await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            }
            else
            {
                var terms = await db.QuestDefinitionRevision.SingleAsync(row => row.AccountId == account.Id);
                terms.Title = "Forged historical terms";
                await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            }

            await transaction.RollbackAsync();
        }

        var replay = await fixture.Commands().Execute(owner, accepted, Start.AddYears(1));
        Assert.Equal(original.Occurrences[0].Occurrence.Quest.Title, replay.Occurrences[0].Occurrence.Quest.Title);
        await using var evidence = fixture.Context();
        Assert.False(await evidence.QuestDefinitionRevision.AnyAsync(row => row.Title == "Forged historical terms"));
    }

    /// <summary>The original marker instant survives UTC conversion and a rejected tracked timestamp edit.</summary>
    /// <returns>Completion after real SaveChanges and fresh-context verification.</returns>
    [Fact]
    public async Task MarkerRetainsOriginalInstantAfterNonUtcWriteAndRejectedTrackedEdit()
    {
        var owner = Identity();
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var marker = new ErasureMarkerEntity { Id = owner.Subject, CreatedAt = Start.ToOffset(TimeSpan.FromHours(5.5)) };
            db.ErasureMarker.Add(marker);
            await db.SaveChangesAsync();
            marker.CreatedAt = marker.CreatedAt.AddTicks(1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using var evidence = fixture.Context();
        var persisted = await evidence.ErasureMarker.SingleAsync(row => row.Id == owner.Subject);
        Assert.Equal(Start, persisted.CreatedAt);
        Assert.Equal(TimeSpan.Zero, persisted.CreatedAt.Offset);
    }

    private static AccountIdentity Identity() => new("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
}
