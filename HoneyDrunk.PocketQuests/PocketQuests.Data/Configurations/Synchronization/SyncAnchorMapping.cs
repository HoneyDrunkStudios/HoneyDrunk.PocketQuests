using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Synchronization;

/// <summary>Maps SyncAnchor fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class SyncAnchorMapping : IEntityTypeConfiguration<SyncAnchorEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SyncAnchorEntity> entity)
    {
        entity.ToTable("SyncAnchor", "pocketquests", table =>
        {
            table.HasComment("One row is one durable trusted clock/visibility proof for an account, device and process. Classification: Restricted. History: mutable; Retain compact proof while valid pending actions can reference it; no wall-clock expiry. Invalidate under lifecycle rules and erase with account. Safe retirement protocol is not implemented here.");
            table.HasCheckConstraint("CK_SyncAnchor_IssuedMutationVersion", "[IssuedMutationVersion] >= 0");
            table.HasCheckConstraint("CK_SyncAnchor_LastOrdinal", "[LastOrdinal] >= 0");
            table.HasCheckConstraint("CK_SyncAnchor_LastElapsedMilliseconds", "[LastElapsedMilliseconds] >= 0");
            table.HasCheckConstraint("CK_SyncAnchor_ServerAtUtc", "DATEPART(TZOFFSET,[ServerAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_DeviceAtUtc", "DATEPART(TZOFFSET,[DeviceAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_RecordedTimeFloorAtUtc", "DATEPART(TZOFFSET,[RecordedTimeFloorAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_InvalidatedAtUtc", "[InvalidatedAt] IS NULL OR DATEPART(TZOFFSET,[InvalidatedAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_SyncAnchor_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_SyncAnchor").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_SyncAnchor_AccountId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DeviceId).HasComment("Client device identifier, bound to the verified account; not an authentication credential.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.BootId).HasComment("Process-session identity used for monotonic clock proof; no silent rewrite across restarts.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ServerAt).HasComment("Trusted server clock at proof issuance. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DeviceAt).HasComment("Device wall clock captured at issuance. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.RecordedTimeFloorAt).HasComment("Fixed issuance floor for already observed account history; never accumulated on refresh. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.IssuedMutationVersion).HasComment("Account version whose visible immutable occurrence revisions this proof authenticates.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.LastOrdinal).HasComment("Largest committed ordinal under this proof.").HasDefaultValueSql("0");
        entity.Property(row => row.LastElapsedMilliseconds).HasColumnType("float(53)").HasComment("Largest committed finite elapsed milliseconds under this proof. SQL float(53) preserves the existing API v1 IEEE-754 double, including fractional milliseconds; never rounded to an integer. Not money, XP, a wall clock or a duration added to the fixed issuance floor.").HasDefaultValueSql("0");
        entity.Property(row => row.InvalidatedAt).HasComment("Lifecycle revocation time; null means not explicitly revoked. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.HasIndex(row => new { row.AccountId, row.DeviceId, row.BootId, row.ServerAt }, "IX_SyncAnchor_DeviceProof");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SyncAnchor_Account");
    }
}
