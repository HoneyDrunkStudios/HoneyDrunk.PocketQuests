using HoneyDrunk.Identity.Abstractions.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Tests.Fixtures;

namespace PocketQuests.SchemaTests;

/// <summary>Exercises real scoped EF workflows under the deployed command-role permissions.</summary>
/// <param name="fixture">Disposable database with canonical role definitions.</param>
public sealed class EfRuntimeRoleTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>A recovered identity can initialize, accept and complete using only the command role.</summary>
    /// <returns>Completion after the actual workflow and persisted lifecycle-link checks.</returns>
    [Fact]
    public async Task RecoveredBeforeInitializationCanAttachFenceAndCommitCommands()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        await fixture.Lifecycle().ObserveActive(new UserRecord(owner.Subject, IdentityProtocol.Active, now.AddDays(-1), 2, now.AddHours(-1)), now);
        await fixture.Execute("CREATE USER pq_ef_runtime_probe WITHOUT LOGIN; ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_ef_runtime_probe;");
        try
        {
            await using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workflow = scope.ServiceProvider.GetRequiredService<TestQuestWorkflow>();
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='pq_ef_runtime_probe';");
            try
            {
                await workflow.Initialize(owner, "Etc/UTC", now);
                var occurrence = Guid.NewGuid();
                await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.Accept, occurrence, QuestId: "PQ-CAT-Q01"), now);
                var completed = await workflow.Execute(owner, new(Guid.NewGuid(), QuestActions.Complete, occurrence), now.AddSeconds(1));
                Assert.Equal(10, completed.OverallXp);
                var account = await db.Account.SingleAsync(row => row.IdentityUserId == owner.Subject);
                Assert.Equal(account.Id, (await db.AccountLifecycleState.SingleAsync(row => row.IdentityUserId == owner.Subject)).AccountId);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("REVERT;");
                await db.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            await fixture.Execute("DROP USER pq_ef_runtime_probe;");
        }
    }

    /// <summary>Private discovery writes and account erasure succeed with deployed runtime roles.</summary>
    /// <returns>Completion after service writes, revision checks and lifecycle cleanup under restricted users.</returns>
    [Fact]
    public async Task SavedPlaceWritesAndErasureUseRuntimePermissions()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        await fixture.Execute("CREATE USER pq_discovery_command WITHOUT LOGIN; ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_discovery_command; CREATE USER pq_discovery_erasure WITHOUT LOGIN; ALTER ROLE pocketquests_lifecycle_runtime ADD MEMBER pq_discovery_erasure;");
        try
        {
            var registration = new ServiceCollection();
            registration.AddQuestDataServices(fixture.Connection);
            PersistenceServices.Register(registration);
            registration.AddScoped<PocketQuests.Services.Accounts.ICurrentAccount>(_ => new RuntimeAccount(owner));
            await using var provider = registration.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var workflow = scope.ServiceProvider.GetRequiredService<TestQuestWorkflow>();
            var places = scope.ServiceProvider.GetRequiredService<PocketQuests.Services.Discoveries.ISavedPlaceService>();
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='pq_discovery_command';");
            try
            {
                await workflow.Initialize(owner, "Etc/UTC", now);
                var request = new PocketQuests.Contracts.Requests.Discoveries.SavePlaceRequest(Guid.NewGuid(), "node/42", "Runtime park", "Parks", "c07", 40m, -74m, "private", false, null, 0);
                var saved = await places.Save(request);
                var edited = await places.Save(request with { ExpectedRevision = saved.Revision, Notes = "updated" });
                await places.Remove(new(request.Id, edited.Revision));
                await places.Save(request with { Id = Guid.NewGuid(), PlaceKey = "node/43" });
                Assert.Single(await places.Read());
                await db.Database.ExecuteSqlRawAsync("REVERT; EXECUTE AS USER='pq_discovery_erasure';");
                var intent = new HoneyDrunk.Identity.Abstractions.AccountLifecycle.LifecycleIntent(owner.Subject, 2, IdentityProtocol.Erasing, now.AddMinutes(1), now.AddHours(1), IdentityProtocol.ConsumerId, new string('e', 64), now);
                await scope.ServiceProvider.GetRequiredService<PocketQuests.Services.Lifecycle.LifecycleService>().ReceiveLifecycle(intent, "private-ack", now.AddMinutes(1));
                Assert.False(await db.SavedPlace.AnyAsync());
                Assert.False(await db.Account.AnyAsync(row => row.IdentityUserId == owner.Subject));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("REVERT;");
                await db.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            await fixture.Execute("DROP USER pq_discovery_command; DROP USER pq_discovery_erasure;");
        }
    }

    private sealed class RuntimeAccount(AccountIdentity owner) : PocketQuests.Services.Accounts.ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity { get; } = new(owner.Issuer, owner.Subject);
    }
}
