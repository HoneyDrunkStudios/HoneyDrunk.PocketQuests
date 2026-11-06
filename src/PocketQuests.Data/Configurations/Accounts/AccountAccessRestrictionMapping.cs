using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.Configurations.Accounts;

/// <summary>Maps the current product access restriction to its DACPAC-owned table.</summary>
public sealed class AccountAccessRestrictionMapping : IEntityTypeConfiguration<AccountAccessRestrictionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountAccessRestrictionEntity> builder)
    {
        builder.ToTable("AccountAccessRestriction", "pocketquests", table =>
        {
            table.HasComment("One row is the current Pocket Quests access restriction for one account. Classification: Restricted. History: mutable current row with actor stamps and rowversion; shared action audit excludes reasons and is erased with the account. Retain only while the product account exists; no temporal copies of reasons.");
            table.HasCheckConstraint("CK_AccountAccessRestriction_Revision", "[Revision]>=1");
            table.HasCheckConstraint("CK_AccountAccessRestriction_Reason", "LEN(LTRIM(RTRIM([Reason])))>0");
            table.HasCheckConstraint("CK_AccountAccessRestriction_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountAccessRestriction_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_AccountAccessRestriction_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_AccountAccessRestriction_CreatedBy", "DATALENGTH([CreatedBy])=30 AND LEFT([CreatedBy],4)='usr_' AND SUBSTRING([CreatedBy],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%'");
            table.HasCheckConstraint("CK_AccountAccessRestriction_ModifiedBy", "DATALENGTH([ModifiedBy])=30 AND LEFT([ModifiedBy],4)='usr_' AND SUBSTRING([ModifiedBy],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%'");
        });
        builder.HasKey(row => row.Id).HasName("PK_AccountAccessRestriction").IsClustered();
        builder.Property(row => row.Id).HasComment("Application-generated stable identifier for the current account moderation record.").ValueGeneratedNever();
        builder.Property(row => row.AccountId).HasComment("Pocket Quests profile whose product access is restricted; not a global Identity suspension.");
        builder.Property(row => row.IsSuspended).HasComment("True denies Pocket Quests use until an authorized explicit reactivation.");
        builder.Property(row => row.Reason).HasComment("Current private operator reason for suspension or reactivation; no previous reason copies are retained.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(500);
        builder.Property(row => row.Revision).HasComment("Positive edit revision checked under the account command lock; stale changes must reload.");
        builder.Property(row => row.CreatedBy).HasComment("Canonical HoneyDrunk.Identity usr_ operator identifier that created this record; not a provider subject or email.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false);
        builder.Property(row => row.ModifiedBy).HasComment("Canonical HoneyDrunk.Identity usr_ operator identifier for the most recent explicit change.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false);
        builder.Property(row => row.CreatedAt).HasComment("UTC creation instant for this current moderation record.").HasPrecision(7);
        builder.Property(row => row.ModifiedAt).HasComment("UTC instant of the most recent explicit moderation change, never before creation.").HasPrecision(7);
        builder.Property(row => row.RowVersion).HasComment("SQL-generated concurrency token, not a timestamp.").IsRowVersion();
        builder.HasAlternateKey(row => row.AccountId).HasName("UQ_AccountAccessRestriction_AccountId");
        builder.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountAccessRestriction_Account");
    }
}
