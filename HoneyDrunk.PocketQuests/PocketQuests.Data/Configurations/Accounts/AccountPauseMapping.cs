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
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ScopeCode).HasColumnType("varchar(12)").HasComment("Account or Category; distinguishes all-category intent from one-category intent.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Paused category; null means account-wide scope.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.StartedAt).HasColumnType("datetimeoffset(7)").HasComment("Effective start of this pause. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EndedAt).HasColumnType("datetimeoffset(7)").HasComment("Effective end of this pause. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CommandReceiptId).HasColumnType("uniqueidentifier").HasComment("Receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.").IsRequired(false).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRequired(true).IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasColumnType("int").HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").IsRequired(true).HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.ScopeCode, row.CategoryId }, "UQ_AccountPause_OpenScope").IsUnique(true).HasFilter("[EndedAt] IS NULL");
        entity.HasIndex(row => new { row.AccountId, row.StartedAt, row.Id }, "IX_AccountPause_Replay").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.CategoryId, "IX_AccountPause_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_AccountPause_FK_CommandReceipt").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_Category");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountPause_CommandReceipt");
    }
}
