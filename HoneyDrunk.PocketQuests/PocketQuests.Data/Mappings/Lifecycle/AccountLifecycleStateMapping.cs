using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.Mappings.Lifecycle;

/// <summary>Maps AccountLifecycleState fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountLifecycleStateMapping : IEntityTypeConfiguration<AccountLifecycleStateEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountLifecycleStateEntity> entity)
    {
        entity.ToTable("AccountLifecycleState", "pocketquests", table =>
        {
            table.HasComment("One row is one latest accepted lifecycle fence for a canonical Identity user, possibly before onboarding. Classification: Restricted. History: mutable; Retain while required to fence the account/pre-onboarding identity. On verified erasure replace with minimal marker; no content history.");
            table.HasCheckConstraint("CK_AccountLifecycleState_Version", "[Version] >= 0");
            table.HasCheckConstraint("CK_AccountLifecycleState_StateCode", "[StateCode] IN ('Active','Inactive','Erasing')");
            table.HasCheckConstraint("CK_AccountLifecycleState_PauseOrder", "[PausedAt] IS NULL OR [PausedAt]<=[EffectiveAt]");
            table.HasCheckConstraint("CK_AccountLifecycleState_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_AccountLifecycleState_PausedAtUtc", "[PausedAt] IS NULL OR DATEPART(TZOFFSET,[PausedAt])=0");
            table.HasCheckConstraint("CK_AccountLifecycleState_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountLifecycleState_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_AccountLifecycleState_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_AccountLifecycleState_IdentityUserId", "DATALENGTH([IdentityUserId])=30 AND LEFT([IdentityUserId],4)='usr_' AND SUBSTRING([IdentityUserId],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%' COLLATE Latin1_General_100_BIN2");
        });
        entity.HasKey(row => row.Id).HasName("PK_AccountLifecycleState").IsClustered();
        entity.HasIndex(row => row.IdentityUserId, "UQ_AccountLifecycleState_IdentityUserId").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.IdentityUserId).HasColumnType("varchar(30)").HasComment("Canonical external Identity user being fenced; never a provider identity.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Local product profile if one exists; null before onboarding or after purge.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.Version).HasColumnType("bigint").HasComment("Highest accepted monotonic Identity lifecycle version; old deliveries cannot reduce it.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.StateCode).HasColumnType("varchar(8)").HasComment("Active, Inactive or Erasing, as defined by the reviewed Identity lifecycle contract.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(8).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.EffectiveAt).HasColumnType("datetimeoffset(7)").HasComment("Authoritative Identity transition instant. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.PausedAt).HasColumnType("datetimeoffset(7)").HasComment("Original deletion-request pause origin, retained across cancellation. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRequired(true).IsRowVersion();
        entity.HasIndex(row => row.AccountId, "UQ_AccountLifecycleState_Account").IsUnique(true).HasFilter("[AccountId] IS NOT NULL");
        entity.HasIndex(row => new { row.AccountId, row.IdentityUserId }, "IX_AccountLifecycleState_FK_Account").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.IdentityUserId }).HasPrincipalKey(row => new { row.Id, row.IdentityUserId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountLifecycleState_Account");
    }
}
