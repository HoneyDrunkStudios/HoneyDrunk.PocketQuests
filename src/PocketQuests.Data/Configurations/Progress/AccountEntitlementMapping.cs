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
    public void Configure(EntityTypeBuilder<AccountEntitlementEntity> builder)
    {
        builder.ToTable("AccountEntitlement", "pocketquests", table =>
        {
            table.HasComment("One row is one account current eligibility for a public achievement, badge or frame. Classification: Restricted. History: projection; Current projection only; no historical full copies. Erase with account.");
            table.HasCheckConstraint("CK_AccountEntitlement_QualifyingCount", "[QualifyingCount] >= 0");
            table.HasCheckConstraint("CK_AccountEntitlement_ProjectionVersion", "[ProjectionVersion] >= 0");
            table.HasCheckConstraint("CK_AccountEntitlement_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountEntitlement_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_AccountEntitlement_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        builder.HasKey(row => row.Id).HasName("PK_AccountEntitlement").IsClustered();
        builder.HasIndex(row => new { row.AccountId, row.Id }, "UQ_AccountEntitlement_AccountId_Id").IsUnique().HasFilter(null);
        builder.HasIndex(row => new { row.AccountId, row.ProfileRewardId }, "UQ_AccountEntitlement_AccountId_ProfileRewardId").IsUnique().HasFilter(null);
        builder.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ProfileRewardId).HasComment("Reward definition whose eligibility is projected.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        builder.Property(row => row.QualifyingCount).HasComment("Nonnegative current count of surviving qualifying contributions.");
        builder.Property(row => row.IsEarned).HasComment("Current derived eligibility; may become false after Undo or rank change.");
        builder.Property(row => row.ProjectionVersion).HasComment("Account projection generation for this reward.");
        builder.Property(row => row.RulesetVersion).HasComment("Reviewed trigger version used for this eligibility.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false);
        builder.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        builder.HasIndex(row => row.ProfileRewardId, "IX_AccountEntitlement_FK_ProfileReward");
        builder.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountEntitlement_Account");
        builder.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => row.ProfileRewardId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountEntitlement_ProfileReward");
    }
}
