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
    public void Configure(EntityTypeBuilder<AccountEntity> builder)
    {
        builder.ToTable("Account", "pocketquests", table =>
        {
            table.HasCheckConstraint("CK_Account_AvatarCode", "[AvatarCode] IS NULL OR (DATALENGTH([AvatarCode])=12 AND [AvatarCode] LIKE 'v1:[0-5]:[0-3]:[0-3]:[0-3]:[0-3]') OR (DATALENGTH([AvatarCode])=16 AND [AvatarCode] LIKE 'v2:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]') OR (DATALENGTH([AvatarCode])=20 AND [AvatarCode] LIKE 'v3:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]:[0-2]:[0-7]')");
            table.HasComment("One row is one personal Pocket Quests profile for a verified canonical Identity user. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_Account_CharacterName", "[CharacterName] IS NULL OR (LEN(LTRIM(RTRIM([CharacterName]))) BETWEEN 1 AND 40 AND DATALENGTH([CharacterName])<=80)");
            table.HasCheckConstraint("CK_Account_PortraitId", "[PortraitId] IS NULL OR [PortraitId] IN ('portrait-1','portrait-2','portrait-3','portrait-4','portrait-5','portrait-6')");
            table.HasCheckConstraint("CK_Account_CharacterPair", "([CharacterName] IS NULL AND [PortraitId] IS NULL) OR ([CharacterName] IS NOT NULL AND [PortraitId] IS NOT NULL)");
            table.HasCheckConstraint("CK_Account_SelectedBadgeKind", "[SelectedBadgeKind] IN ('Badge')");
            table.HasCheckConstraint("CK_Account_SelectedFrameKind", "[SelectedFrameKind] IN ('Frame')");
            table.HasCheckConstraint("CK_Account_IdentityUserId", "DATALENGTH([IdentityUserId])=30 AND LEFT([IdentityUserId],4)='usr_' AND SUBSTRING([IdentityUserId],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%' COLLATE Latin1_General_100_BIN2");
            table.HasCheckConstraint("CK_Account_ProjectionVersion", "[MutationVersion]>=0 AND [ProjectionVersion]>=0 AND [ProjectionVersion]<=[MutationVersion]");
            table.HasCheckConstraint("CK_Account_LastRecordedAtUtc", "DATEPART(TZOFFSET,[LastRecordedAt])=0");
            table.HasCheckConstraint("CK_Account_ProjectionAsOfAtUtc", "DATEPART(TZOFFSET,[ProjectionAsOfAt])=0");
            table.HasCheckConstraint("CK_Account_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_Account_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_Account_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_Account_FounderPlusGrantedAtUtc", "[FounderPlusGrantedAt] IS NULL OR DATEPART(TZOFFSET,[FounderPlusGrantedAt])=0");
            table.HasCheckConstraint("CK_Account_StripeSubscriptionLink", "[StripeSubscriptionId] IS NULL OR ([StripeCustomerId] IS NOT NULL AND LEFT([StripeSubscriptionId],4)='sub_' AND LEN([StripeSubscriptionId])>4)");
            table.HasCheckConstraint("CK_Account_StripeCustomerId", "[StripeCustomerId] IS NULL OR (LEN([StripeCustomerId]) BETWEEN 5 AND 80 AND DATALENGTH([StripeCustomerId])=LEN([StripeCustomerId]) AND LEFT([StripeCustomerId],4)='cus_' AND SUBSTRING([StripeCustomerId],5,76) NOT LIKE '%[^0-9A-Za-z]%')");
            table.HasCheckConstraint("CK_Account_PlusCheckoutReservation", "([PlusCheckoutStartedAt] IS NULL AND [PlusCheckoutInterval] IS NULL AND [StripeCheckoutSessionId] IS NULL) OR ([PlusCheckoutStartedAt] IS NOT NULL AND DATEPART(TZOFFSET,[PlusCheckoutStartedAt])=0 AND [PlusCheckoutInterval] IS NOT NULL AND [PlusCheckoutInterval] IN ('monthly','yearly'))");
        });
        builder.Property(row => row.AvatarCode).HasComment("Versioned full-body appearance: v1 stores skin, hair color, hairstyle, outfit and accessory; v2 appends face and build with expanded bounded choices. Null means starter appearance. Private account preference, erased with account history. Version v3 appends an extra starter accessory and scarf color; earlier versions retain their exact meanings.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(20).IsUnicode(false);
        builder.HasKey(row => row.Id).HasName("PK_Account").IsClustered();
        builder.HasIndex(row => row.IdentityUserId, "UQ_Account_IdentityUserId").IsUnique().HasFilter(null);
        builder.HasIndex(row => row.StripeCustomerId, "UQ_Account_StripeCustomerId").IsUnique().HasFilter("[StripeCustomerId] IS NOT NULL");
        builder.Property(row => row.FounderPlusGrantedAt).HasPrecision(7).HasComment("UTC time of the complimentary lifetime Plus grant. Null means no founder grant. Erased with the account.");
        builder.Property(row => row.StripeCustomerId).HasMaxLength(80).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").HasComment("External Stripe Customer ID for the account. Null means billing has not been initialized. Restricted data erased with the account.");
        builder.Property(row => row.StripeSubscriptionId).HasMaxLength(80).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").HasComment("External Stripe Subscription ID linked by verified billing events. Null means no linked subscription. Restricted data erased with the account.");
        builder.Property(row => row.PlusCheckoutStartedAt).HasPrecision(7).HasComment("UTC time of the current Plus Checkout reservation. Null means no reservation. Replaced on a new attempt and erased with the account.");
        builder.Property(row => row.PlusCheckoutInterval).HasMaxLength(7).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").HasComment("Monthly or yearly interval reserved for hosted Checkout. Null means no reservation.");
        builder.Property(row => row.StripeCheckoutSessionId).HasMaxLength(120).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").HasComment("Stripe Checkout Session ID for the current reservation once created. Null means creation has not been confirmed. Restricted data erased with the account.");
        builder.HasAlternateKey(row => new { row.Id, row.IdentityUserId }).HasName("UQ_Account_Id_IdentityUserId");
        builder.Property(row => row.CharacterName).HasComment("Player-chosen character display name, 1 to 40 UTF-16 code units; null means the player has not chosen a character. Restricted account data; erase with the account.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40);
        builder.Property(row => row.PortraitId).HasComment("Bundled portrait code portrait-1 through portrait-6; null means the player has not chosen a character. No remote URL or upload; erase with the account.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(16).IsUnicode(false);
        builder.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.IdentityUserId).HasComment("Canonical never-recycled usr_ identifier returned by HoneyDrunk.Identity, not a provider subject or email.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.TimeZoneId).HasComment("Selected IANA timezone used for personal calendar calculations, not a UTC offset.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false);
        builder.Property(row => row.IsOnboardingComplete).HasComment("Whether explicit product onboarding completed.").HasDefaultValueSql("0");
        builder.Property(row => row.HasExpiryWarnings).HasComment("Whether the account requested optional grouped expiry warnings; device OS permission is separate.").HasDefaultValueSql("0");
        builder.Property(row => row.IsAccountPaused).HasComment("Current explicit all-category pause setting; effective intervals are stored separately.").HasDefaultValueSql("0");
        builder.Property(row => row.SelectedBadgeId).HasComment("Selected badge code; null means no selected badge.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        builder.Property(row => row.SelectedBadgeKind).HasComment("Constant Badge discriminator for the typed reward FK.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).HasDefaultValueSql("'Badge'").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.SelectedFrameId).HasComment("Selected frame code; null means no selected frame.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        builder.Property(row => row.SelectedFrameKind).HasComment("Constant Frame discriminator for the typed reward FK.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).HasDefaultValueSql("'Frame'").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.LastRecordedAt).HasComment("Fixed account logical-time high-water mark; never a future deadline. UTC instant.").HasPrecision(7);
        builder.Property(row => row.MutationVersion).HasComment("Monotonic committed account mutation number; issued anchors retain this visibility version.").HasDefaultValueSql("0");
        builder.Property(row => row.ProjectionVersion).HasComment("Mutation version fully reflected by the current materialized progress projection.").HasDefaultValueSql("0");
        builder.Property(row => row.ProjectionAsOfAt).HasComment("Clock instant through which time-driven projection state is reconciled. UTC instant.").HasPrecision(7);
        builder.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        builder.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        builder.Property(row => row.HasPendingReconciliation).HasComment("Whether bounded recurrence delivery has more work at the latest requested clock; ProjectionAsOfAt advances only when that work is complete.").HasDefaultValueSql("0");
        builder.HasIndex(row => new { row.SelectedBadgeId, row.SelectedBadgeKind }, "IX_Account_FK_ProfileReward_SelectedBadge");
        builder.HasIndex(row => new { row.SelectedFrameId, row.SelectedFrameKind }, "IX_Account_FK_ProfileReward_SelectedFrame");
        builder.HasIndex(row => new { row.ProjectionAsOfAt, row.Id }, "IX_Account_ReconciliationClock").IncludeProperties("IdentityUserId");
        builder.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => new { row.SelectedBadgeId, row.SelectedBadgeKind }).HasPrincipalKey(row => new { row.Id, row.KindCode }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Account_ProfileReward_SelectedBadge");
        builder.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => new { row.SelectedFrameId, row.SelectedFrameKind }).HasPrincipalKey(row => new { row.Id, row.KindCode }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Account_ProfileReward_SelectedFrame");
    }
}
