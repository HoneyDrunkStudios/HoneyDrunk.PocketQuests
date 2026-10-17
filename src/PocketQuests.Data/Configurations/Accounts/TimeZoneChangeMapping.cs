using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Accounts;

/// <summary>Maps TimeZoneChange fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class TimeZoneChangeMapping : IEntityTypeConfiguration<TimeZoneChangeEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TimeZoneChangeEntity> entity)
    {
        entity.ToTable("TimeZoneChange", "pocketquests", table =>
        {
            table.HasComment("One row is one recorded selection of a new personal calendar timezone. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_TimeZoneChange_Changed", "[FromTimeZoneId]<>[ToTimeZoneId]");
            table.HasCheckConstraint("CK_TimeZoneChange_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_TimeZoneChange_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_TimeZoneChange").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_TimeZoneChange_AccountId_Id").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.FromTimeZoneId).HasComment("IANA zone before the change.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ToTimeZoneId).HasComment("IANA zone selected by the account.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EffectiveAt).HasComment("Recorded time of the timezone change. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandReceiptId).HasComment("Receipt for the command that caused this fact.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.EffectiveAt, row.Id }, "IX_TimeZoneChange_Replay");
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_TimeZoneChange_FK_CommandReceipt");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TimeZoneChange_Account");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TimeZoneChange_CommandReceipt");
    }
}
