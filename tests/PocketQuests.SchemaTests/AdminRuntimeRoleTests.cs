using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Administration;
using PocketQuests.Tests.Fixtures;

namespace PocketQuests.SchemaTests;

/// <summary>Moderation and erasure run with the deployed runtime grants.</summary>
/// <param name="fixture">Disposable deployed database.</param>
public sealed class AdminRuntimeRoleTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>Suspension and reactivation commit, then erasure removes the current restriction and owned audit.</summary>
    /// <returns>Completion after real services operate under restricted database users.</returns>
    [Fact]
    public async Task ModerationAndErasureUseRestrictedRuntimeGrants()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = new AccountIdentity("honeydrunk-identity", "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        var other = new AccountIdentity(owner.Issuer, "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant());
        await fixture.Execute("CREATE USER pq_moderation_command WITHOUT LOGIN; ALTER ROLE pocketquests_command_runtime ADD MEMBER pq_moderation_command; CREATE USER pq_moderation_erasure WITHOUT LOGIN; ALTER ROLE pocketquests_lifecycle_runtime ADD MEMBER pq_moderation_erasure;");
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
            var moderation = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='pq_moderation_command';");
            try
            {
                await workflow.Initialize(owner, "Etc/UTC", now);
                await workflow.Initialize(other, "Etc/UTC", now);
                var suspended = await moderation.SetAccess(new(other.Subject, true, "Runtime test suspension", 0), default);
                Assert.True(suspended.Suspended);
                var active = await moderation.SetAccess(new(other.Subject, false, "Runtime test reactivation", suspended.Revision), default);
                Assert.False(active.Suspended);
                Assert.True(await db.AccountAccessRestriction.AnyAsync());
                await db.Database.ExecuteSqlRawAsync("REVERT; EXECUTE AS USER='pq_moderation_erasure';");
                var lifecycle = scope.ServiceProvider.GetRequiredService<PocketQuests.Services.Lifecycle.LifecycleService>();
                foreach (var identity in new[] { owner, other })
                {
                    var intent = new HoneyDrunk.Identity.Abstractions.AccountLifecycle.LifecycleIntent(identity.Subject, 2, IdentityProtocol.Erasing, now.AddMinutes(1), now.AddHours(1), IdentityProtocol.ConsumerId, new string('c', 64), now);
                    await lifecycle.ReceiveLifecycle(intent, "private-ack", now.AddMinutes(1));
                }

                Assert.False(await db.AccountAccessRestriction.AnyAsync());
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
            await fixture.Execute("DROP USER pq_moderation_command; DROP USER pq_moderation_erasure;");
        }
    }

    private sealed class StudioAccount(AccountIdentity identity) : PocketQuests.Services.Accounts.ICurrentAccount
    {
        public PocketQuests.Contracts.Models.Accounts.AccountIdentity Identity { get; } = new(identity.Issuer, identity.Subject);
    }
}
