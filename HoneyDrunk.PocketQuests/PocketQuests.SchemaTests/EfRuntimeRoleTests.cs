using HoneyDrunk.Identity.Abstractions.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Services.Quests;

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
            var workflow = scope.ServiceProvider.GetRequiredService<IQuestService>();
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
}
