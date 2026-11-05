using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Administration;
using PocketQuests.Services.Commands.Mapping;
using PocketQuests.Tests.Fixtures;

namespace PocketQuests.SchemaTests;

/// <summary>Studio mutations and erasure run with the deployed runtime grants.</summary>
/// <param name="fixture">Disposable deployed database.</param>
public sealed class SystemQuestRuntimeRoleTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    private static readonly int[] HistoryRevisionNumbers = [2, 1];

    /// <summary>Draft creation/editing succeeds, immutable content stays protected and both authors and other accounts can be erased.</summary>
    /// <returns>Completion after real services operate under restricted database users.</returns>
    [Fact]
    public async Task SystemQuestAuthoringAndErasureUseRestrictedRuntimeGrants()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var other = new AccountIdentity(owner.Issuer, "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        await fixture.Execute("CREATE USER pq_studio_command WITHOUT LOGIN; ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_studio_command; CREATE USER pq_studio_erasure WITHOUT LOGIN; ALTER ROLE pocketquests_lifecycle_runtime ADD MEMBER pq_studio_erasure;");
        try
        {
            var registrations = new ServiceCollection();
            registrations.AddQuestDataServices(fixture.Connection);
            PersistenceServices.Register(registrations);
            registrations.AddScoped<PocketQuests.Services.Accounts.ICurrentAccount>(_ => new StudioAccount(owner));
            registrations.Configure<AdminAccessOptions>(options => options.UserId = owner.Subject);
            await using var provider = registrations.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workflow = scope.ServiceProvider.GetRequiredService<TestQuestWorkflow>();
            var studio = scope.ServiceProvider.GetRequiredService<IAdminSystemQuestService>();
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='pq_studio_command';");
            try
            {
                await workflow.Initialize(owner, "Etc/UTC", now);
                await workflow.Initialize(other, "Etc/UTC", now);
                var id = Guid.NewGuid();
                var quest = Catalog.Quests[0] with { Id = "PQ-ADM-" + id.ToString("N") };
                var draft = await studio.Save(new(id, quest.ToInput()), default);
                var edit = await studio.Save(new(id, (quest with { Title = "Runtime edited title" }).ToInput(), draft.Version), default);
                Assert.Equal(2, edit.DraftNumber);
                var live = await studio.Publish(new(id, edit.Version, edit.DraftSystemQuestRevisionId, true), default);
                Assert.Equal(edit.DraftSystemQuestRevisionId, live.PublishedSystemQuestRevisionId);
                var history = await studio.History(new(id), default);
                Assert.Equal(HistoryRevisionNumbers, history.Revisions.Select(row => row.Number));
                Assert.Equal(draft.DraftSystemQuestRevisionId, history.Revisions[1].Id);
                await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE pocketquests.SystemQuestRevision SET SnapshotJson=N'{{}}';"));
                await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM pocketquests.SystemQuestRevision;"));
                await db.Database.ExecuteSqlRawAsync("REVERT; EXECUTE AS USER='pq_studio_erasure';");
                var lifecycle = scope.ServiceProvider.GetRequiredService<PocketQuests.Services.Lifecycle.LifecycleService>();
                foreach (var identity in new[] { owner, other })
                {
                    var intent = new HoneyDrunk.Identity.Abstractions.AccountLifecycle.LifecycleIntent(identity.Subject, 2, IdentityProtocol.Erasing, now.AddMinutes(1), now.AddHours(1), IdentityProtocol.ConsumerId, new string('c', 64), now);
                    await lifecycle.ReceiveLifecycle(intent, "private-ack", now.AddMinutes(1));
                }

                var head = await db.SystemQuest.AsNoTracking().SingleAsync(row => row.Id == quest.Id);
                Assert.Null(head.CreatedBy);
                Assert.Null(head.ModifiedBy);
                Assert.Equal(edit.DraftSystemQuestRevisionId, head.PublishedSystemQuestRevisionId);
                Assert.Equal(2, await db.SystemQuestRevision.CountAsync(row => row.SystemQuestId == quest.Id));
                Assert.False(await db.Account.AnyAsync(row => row.IdentityUserId == owner.Subject || row.IdentityUserId == other.Subject));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("REVERT;");
                await db.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            await fixture.Execute("DROP USER pq_studio_command; DROP USER pq_studio_erasure;");
        }
    }

    private sealed class StudioAccount(AccountIdentity identity) : PocketQuests.Services.Accounts.ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity { get; } = new(identity.Issuer, identity.Subject);
    }
}
