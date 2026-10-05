using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.Configurations.Accounts;

/// <summary>Maps Account fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountMapping : IEntityTypeConfiguration<AccountEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountEntity> entity)
    {
        entity.ToTable("Account", "pocketquests", table =>
        {
            table.HasComment("One row is one personal Pocket Quests profile for a verified canonical Identity user. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_Account_SelectedBadgeKind", "[SelectedBadgeKind] IN ('Badge')");
            table.HasCheckConstraint("CK_Account_SelectedFrameKind", "[SelectedFrameKind] IN ('Frame')");
            table.HasCheckConstraint("CK_Account_IdentityUserId", "DATALENGTH([IdentityUserId])=30 AND LEFT([IdentityUserId],4)='usr_' AND SUBSTRING([IdentityUserId],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%' COLLATE Latin1_General_100_BIN2");
            table.HasCheckConstraint("CK_Account_ProjectionVersion", "[MutationVersion]>=0 AND [ProjectionVersion]>=0 AND [ProjectionVersion]<=[MutationVersion]");
            table.HasCheckConstraint("CK_Account_LastRecordedAtUtc", "DATEPART(TZOFFSET,[LastRecordedAt])=0");
            table.HasCheckConstraint("CK_Account_ProjectionAsOfAtUtc", "DATEPART(TZOFFSET,[ProjectionAsOfAt])=0");
            table.HasCheckConstraint("CK_Account_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_Account_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_Account_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_Account").IsClustered();
        entity.HasIndex(row => row.IdentityUserId, "UQ_Account_IdentityUserId").IsUnique().HasFilter(null);
        entity.HasAlternateKey(row => new { row.Id, row.IdentityUserId }).HasName("UQ_Account_Id_IdentityUserId");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.IdentityUserId).HasComment("Canonical never-recycled usr_ identifier returned by HoneyDrunk.Identity, not a provider subject or email.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.TimeZoneId).HasComment("Selected IANA timezone used for personal calendar calculations, not a UTC offset.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false);
        entity.Property(row => row.IsOnboardingComplete).HasComment("Whether explicit product onboarding completed.").HasDefaultValueSql("0");
        entity.Property(row => row.HasExpiryWarnings).HasComment("Whether the account requested optional grouped expiry warnings; device OS permission is separate.").HasDefaultValueSql("0");
        entity.Property(row => row.IsAccountPaused).HasComment("Current explicit all-category pause setting; effective intervals are stored separately.").HasDefaultValueSql("0");
        entity.Property(row => row.SelectedBadgeId).HasComment("Selected badge code; null means no selected badge.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.SelectedBadgeKind).HasComment("Constant Badge discriminator for the typed reward FK.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).HasDefaultValueSql("'Badge'").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.SelectedFrameId).HasComment("Selected frame code; null means no selected frame.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.SelectedFrameKind).HasComment("Constant Frame discriminator for the typed reward FK.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).HasDefaultValueSql("'Frame'").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.LastRecordedAt).HasComment("Fixed account logical-time high-water mark; never a future deadline. UTC instant.").HasPrecision(7);
        entity.Property(row => row.MutationVersion).HasComment("Monotonic committed account mutation number; issued anchors retain this visibility version.").HasDefaultValueSql("0");
        entity.Property(row => row.ProjectionVersion).HasComment("Mutation version fully reflected by the current materialized progress projection.").HasDefaultValueSql("0");
        entity.Property(row => row.ProjectionAsOfAt).HasComment("Clock instant through which time-driven projection state is reconciled. UTC instant.").HasPrecision(7);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.HasPendingReconciliation).HasComment("Whether bounded recurrence delivery has more work at the latest requested clock; ProjectionAsOfAt advances only when that work is complete.").HasDefaultValueSql("0");
        entity.HasIndex(row => new { row.SelectedBadgeId, row.SelectedBadgeKind }, "IX_Account_FK_ProfileReward_SelectedBadge");
        entity.HasIndex(row => new { row.SelectedFrameId, row.SelectedFrameKind }, "IX_Account_FK_ProfileReward_SelectedFrame");
        entity.HasIndex(row => new { row.ProjectionAsOfAt, row.Id }, "IX_Account_ReconciliationClock").IncludeProperties("IdentityUserId");
        entity.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => new { row.SelectedBadgeId, row.SelectedBadgeKind }).HasPrincipalKey(row => new { row.Id, row.KindCode }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Account_ProfileReward_SelectedBadge");
        entity.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => new { row.SelectedFrameId, row.SelectedFrameKind }).HasPrincipalKey(row => new { row.Id, row.KindCode }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Account_ProfileReward_SelectedFrame");
    }
}
