using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data;
using PocketQuests.Data.DataServices;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.SchemaTests;

/// <summary>Exercises ordinary EF persistence against the disposable DACPAC database.</summary>
/// <param name="fixture">An independently deployed test database.</param>
public sealed class EfDataServiceSqlTests(SchemaFixture fixture) : IClassFixture<SchemaFixture>
{
    /// <summary>Standard CRUD persists across scopes and returns SQL-generated rowversion values.</summary>
    /// <returns>Completion after insert, update and delete verification.</returns>
    [Fact]
    public async Task CrudPersistsAcrossScopesWithGeneratedConcurrencyTokens()
    {
        await using var provider = Provider();
        var account = NewAccount();
        var skill = NewSkill(account.Id, "Original name");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
            var skills = scope.ServiceProvider.GetRequiredService<ICustomSkillDataService>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await accounts.AddAsync(account);
            await skills.AddAsync(skill);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            Assert.Equal(8, account.RowVersion.Length);
            Assert.Equal(8, skill.RowVersion.Length);
            Assert.Equal("Badge", account.SelectedBadgeKind);
            Assert.Equal("Frame", account.SelectedFrameKind);
        }

        var previousVersion = skill.RowVersion.ToArray();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
            var skills = scope.ServiceProvider.GetRequiredService<ICustomSkillDataService>();
            Assert.Equal(account.Id, (await accounts.GetByIdentityUserId(account.IdentityUserId))!.Id);
            Assert.Null(await accounts.GetByIdentityUserId("usr_00000000000000000000000000"));
            var found = Assert.Single(await skills.GetByAccountId(account.Id));
            Assert.Equal(EntityState.Unchanged, db.Entry(found).State);
            Assert.Same(found, await skills.FindByIdAsync(skill.Id));
            found.Name = "Changed name";
            found.NormalizedName = "CHANGED NAME";
            found.Revision++;
            found.ModifiedAt = found.ModifiedAt.AddSeconds(1);
            skills.Update(found);
            await db.SaveChangesAsync();
            Assert.False(previousVersion.AsSpan().SequenceEqual(found.RowVersion));
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var skills = scope.ServiceProvider.GetRequiredService<ICustomSkillDataService>();
            var persisted = await skills.FindByIdAsync(skill.Id);
            Assert.NotNull(persisted);
            Assert.Equal("Changed name", persisted.Name);
            skills.Remove(persisted);
            await db.SaveChangesAsync();
        }

        await using var read = Context();
        Assert.False(await read.CustomSkill.AnyAsync(row => row.Id == skill.Id));
        Assert.True(await read.Account.AnyAsync(row => row.Id == account.Id));
    }

    /// <summary>Rolling back an explicit transaction also removes an earlier successful SaveChanges.</summary>
    /// <returns>Completion after checking both tables from a new context.</returns>
    [Fact]
    public async Task FailedSecondSaveCanRollBackTheEntireTransaction()
    {
        var account = NewAccount();
        var invalidSkill = NewSkill(account.Id, " ");
        await using (var db = Context())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await new AccountDataService(db).AddAsync(account);
            await db.SaveChangesAsync();
            await new CustomSkillDataService(db).AddAsync(invalidSkill);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
            await transaction.RollbackAsync();
        }

        await using var read = Context();
        Assert.False(await read.Account.AnyAsync(row => row.Id == account.Id));
        Assert.False(await read.CustomSkill.AnyAsync(row => row.Id == invalidSkill.Id));
    }

    /// <summary>A stale account token rejects all writes in the same SaveChanges transaction.</summary>
    /// <returns>Completion after verifying the winner and absence of the losing companion insert.</returns>
    [Fact]
    public async Task RowVersionConflictDoesNotCommitCompanionChanges()
    {
        var account = await SeedAccount();
        await using var first = Context();
        await using var second = Context();
        var winner = await new AccountDataService(first).FindByIdAsync(account.Id);
        var stale = await new AccountDataService(second).FindByIdAsync(account.Id);
        Assert.NotNull(winner);
        Assert.NotNull(stale);
        winner.HasExpiryWarnings = true;
        winner.ModifiedAt = winner.ModifiedAt.AddSeconds(1);
        await first.SaveChangesAsync();
        stale.IsOnboardingComplete = true;
        stale.ModifiedAt = stale.ModifiedAt.AddSeconds(2);
        var losingSkill = NewSkill(account.Id, "Must roll back");
        await new CustomSkillDataService(second).AddAsync(losingSkill);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var read = Context();
        var persisted = await read.Account.SingleAsync(row => row.Id == account.Id);
        Assert.True(persisted.HasExpiryWarnings);
        Assert.False(persisted.IsOnboardingComplete);
        Assert.False(await read.CustomSkill.AnyAsync(row => row.Id == losingSkill.Id));
    }

    /// <summary>EF writes retain composite ownership FKs even when the referenced UUID exists.</summary>
    /// <returns>Completion after rejecting a cross-account series reference.</returns>
    [Fact]
    public async Task CompositeForeignKeyRejectsAnotherAccountsDefinition()
    {
        var owner = await SeedAccount();
        var other = await SeedAccount();
        var definition = new QuestDefinitionEntity { Id = Guid.NewGuid(), AccountId = owner.Id, Revision = 1 };
        await using (var db = Context())
        {
            await new QuestDefinitionDataService(db).AddAsync(definition);
            await db.SaveChangesAsync();
        }

        await using var invalid = Context();
        await new QuestSeriesDataService(invalid).AddAsync(new QuestSeriesEntity
        {
            Id = Guid.NewGuid(), AccountId = other.Id, QuestDefinitionId = definition.Id, Revision = 1,
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, sql.Number);
        Assert.Contains("FK_QuestSeries_QuestDefinition", sql.Message, StringComparison.Ordinal);
    }

    /// <summary>Names are reserved per account, and account queries return only their requested rows.</summary>
    /// <returns>Completion after uniqueness and account-filter checks.</returns>
    [Fact]
    public async Task SkillUniquenessAndQueriesRemainAccountScoped()
    {
        var owner = await SeedAccount();
        var other = await SeedAccount();
        await using (var db = Context())
        {
            var data = new CustomSkillDataService(db);
            await data.AddAsync(NewSkill(owner.Id, "Shared name"));
            await data.AddAsync(NewSkill(other.Id, "Shared name"));
            await db.SaveChangesAsync();
        }

        await using (var read = Context())
        {
            var data = new CustomSkillDataService(read);
            Assert.Equal(owner.Id, Assert.Single(await data.GetByAccountId(owner.Id)).AccountId);
            Assert.Equal(other.Id, Assert.Single(await data.GetByAccountId(other.Id)).AccountId);
        }

        await using var invalid = Context();
        await new CustomSkillDataService(invalid).AddAsync(NewSkill(owner.Id, "Shared name"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
        Assert.Contains(Assert.IsType<SqlException>(error.InnerException).Number, new[] { 2601, 2627 });
    }

    private static AccountEntity NewAccount()
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(), IdentityUserId = "usr_" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant(),
            TimeZoneId = "UTC", LastRecordedAt = now, ProjectionAsOfAt = now, CreatedAt = now, ModifiedAt = now,
        };
    }

    private static CustomSkillEntity NewSkill(Guid accountId, string name) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, Name = name, NormalizedName = name.ToUpperInvariant(),
        NameNormalizationVersion = 1, Revision = 1,
    };

    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(fixture.Connection).Options);

    private ServiceProvider Provider() => new ServiceCollection().AddQuestDataServices(fixture.Connection)
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private async Task<AccountEntity> SeedAccount()
    {
        var account = NewAccount();
        await using var db = Context();
        await new AccountDataService(db).AddAsync(account);
        await db.SaveChangesAsync();
        return account;
    }
}
