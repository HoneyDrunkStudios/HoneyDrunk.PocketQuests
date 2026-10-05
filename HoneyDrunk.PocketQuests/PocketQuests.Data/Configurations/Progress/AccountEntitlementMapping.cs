using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.Configurations.Progress;

/// <summary>Maps AccountEntitlement fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountEntitlementMapping : IEntityTypeConfiguration<AccountEntitlementEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountEntitlementEntity> entity)
    {
        entity.ToTable("AccountEntitlement", "pocketquests", table =>
        {
            table.HasComment("One row is one account current eligibility for a public achievement, badge or frame. Classification: Restricted. History: projection; Current projection only; no historical full copies. Erase with account.");
            table.HasCheckConstraint("CK_AccountEntitlement_QualifyingCount", "[QualifyingCount] >= 0");
            table.HasCheckConstraint("CK_AccountEntitlement_ProjectionVersion", "[ProjectionVersion] >= 0");
            table.HasCheckConstraint("CK_AccountEntitlement_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountEntitlement_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_AccountEntitlement_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_AccountEntitlement").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_AccountEntitlement_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.ProfileRewardId }, "UQ_AccountEntitlement_AccountId_ProfileRewardId").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ProfileRewardId).HasColumnType("varchar(40)").HasComment("Reward definition whose eligibility is projected.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QualifyingCount).HasColumnType("int").HasComment("Nonnegative current count of surviving qualifying contributions.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.IsEarned).HasColumnType("bit").HasComment("Current derived eligibility; may become false after Undo or rank change.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ProjectionVersion).HasColumnType("bigint").HasComment("Account projection generation for this reward.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RulesetVersion).HasColumnType("varchar(32)").HasComment("Reviewed trigger version used for this eligibility.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => row.ProfileRewardId, "IX_AccountEntitlement_FK_ProfileReward").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountEntitlement_Account");
        entity.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => row.ProfileRewardId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountEntitlement_ProfileReward");
    }
}
