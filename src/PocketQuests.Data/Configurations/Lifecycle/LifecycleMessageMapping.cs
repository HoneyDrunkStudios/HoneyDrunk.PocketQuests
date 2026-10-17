using HoneyDrunk.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.Configurations.Lifecycle;

/// <summary>Maps LifecycleMessage fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class LifecycleMessageMapping : IEntityTypeConfiguration<LifecycleMessageEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LifecycleMessageEntity> entity)
    {
        entity.ToTable("LifecycleMessage", "pocketquests", table =>
        {
            table.HasComment("One row is one short-lived acknowledgment-envelope ownership binding for private Identity delivery. Classification: Restricted. History: plumbing; Delete after dispatch or within one hour. May outlive Account only long enough to complete the current private acknowledgment.");
            table.HasCheckConstraint("CK_LifecycleMessage_LifecycleVersion", "[LifecycleVersion] >= 1");
            table.HasCheckConstraint("CK_LifecycleMessage_Expiry", "[ExpiresAt]>[CreatedAt] AND [ExpiresAt]<=DATEADD(hour,1,[CreatedAt])");
            table.HasCheckConstraint("CK_LifecycleMessage_ExpiresAtUtc", "DATEPART(TZOFFSET,[ExpiresAt])=0");
            table.HasCheckConstraint("CK_LifecycleMessage_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_LifecycleMessage_IdentityUserId", "DATALENGTH([IdentityUserId])=30 AND LEFT([IdentityUserId],4)='usr_' AND SUBSTRING([IdentityUserId],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%' COLLATE Latin1_General_100_BIN2");
        });
        entity.HasKey(row => row.Id).HasName("PK_LifecycleMessage").IsClustered();
        entity.HasIndex(row => row.OutboxMessageId, "UQ_LifecycleMessage_OutboxMessageId").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.IdentityUserId).HasComment("Canonical external user whose lifecycle acknowledgment this is.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Product account if still present; null before onboarding or after purge.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.LifecycleVersion).HasComment("Identity transition version being acknowledged.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OutboxMessageId).HasComment("Shared Data.Outbox row containing the acknowledgment capability and payload.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ExpiresAt).HasComment("Hard delivery-envelope expiration; maximum one hour after creation, matching current protocol. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.IdentityUserId, row.LifecycleVersion }, "IX_LifecycleMessage_OwnerVersion");
        entity.HasIndex(row => new { row.ExpiresAt, row.Id }, "IX_LifecycleMessage_Retention");
        entity.HasIndex(row => new { row.AccountId, row.IdentityUserId }, "IX_LifecycleMessage_FK_Account");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.IdentityUserId }).HasPrincipalKey(row => new { row.Id, row.IdentityUserId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LifecycleMessage_Account");
        entity.HasOne<OutboxMessage>().WithMany().HasForeignKey(row => row.OutboxMessageId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LifecycleMessage_OutboxMessages");
    }
}
