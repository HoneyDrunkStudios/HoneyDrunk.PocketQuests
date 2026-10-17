using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Accounts;

/// <summary>Maps AccountPause fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountPauseMapping : IEntityTypeConfiguration<AccountPauseEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountPauseEntity> entity)
    {
        entity.ToTable("AccountPause", "pocketquests", table =>
        {
            table.HasComment("One row is one explicit account/category pause interval. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_AccountPause_Scope", "([ScopeCode]='Account' AND [CategoryId] IS NULL) OR ([ScopeCode]='Category' AND [CategoryId] IS NOT NULL)");
            table.HasCheckConstraint("CK_AccountPause_Interval", "[EndedAt] IS NULL OR [EndedAt]>=[StartedAt]");
            table.HasCheckConstraint("CK_AccountPause_StartedAtUtc", "DATEPART(TZOFFSET,[StartedAt])=0");
            table.HasCheckConstraint("CK_AccountPause_EndedAtUtc", "[EndedAt] IS NULL OR DATEPART(TZOFFSET,[EndedAt])=0");
            table.HasCheckConstraint("CK_AccountPause_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountPause_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_AccountPause_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_AccountPause_CreationOrdinal", "[CreationOrdinal]>=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_AccountPause").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_AccountPause_AccountId_Id").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ScopeCode).HasComment("Account or Category; distinguishes all-category intent from one-category intent.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CategoryId).HasComment("Paused category; null means account-wide scope.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.StartedAt).HasComment("Effective start of this pause. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EndedAt).HasComment("Effective end of this pause. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.CommandReceiptId).HasComment("Receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.ScopeCode, row.CategoryId }, "UQ_AccountPause_OpenScope").IsUnique().HasFilter("[EndedAt] IS NULL");
        entity.HasIndex(row => new { row.AccountId, row.StartedAt, row.Id }, "IX_AccountPause_Replay");
        entity.HasIndex(row => row.CategoryId, "IX_AccountPause_FK_Category");
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_AccountPause_FK_CommandReceipt");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_Category");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_CommandReceipt");
    }
}
