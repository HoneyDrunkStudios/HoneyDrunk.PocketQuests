using HoneyDrunk.Audit.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;

namespace PocketQuests.Data.Mappings.Accounts;

/// <summary>Maps AccountAuditRecord fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountAuditRecordMapping : IEntityTypeConfiguration<AccountAuditRecordEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountAuditRecordEntity> entity)
    {
        entity.ToTable("AccountAuditRecord", "pocketquests", table =>
        {
            table.HasComment("One row is one ownership association between a personal account and its canonical shared audit record. Classification: Restricted. History: event; Retain with the linked personal audit record. Delete association and owned audit record atomically on approved final erasure; no content duplicated.");
            table.HasCheckConstraint("CK_AccountAuditRecord_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => new { row.AccountId, row.AuditRecordId }).HasName("PK_AccountAuditRecord").IsClustered();
        entity.HasIndex(row => row.AuditRecordId, "UQ_AccountAuditRecord_AuditRecordId").IsUnique().HasFilter(null);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AuditRecordId).HasColumnType("varchar(32)").HasComment("Canonical shared AuditRecord ID; envelope content and shape remain Audit-owned. Uses DATABASE_DEFAULT collation to match the unchanged shared AuditRecords.Id.").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountAuditRecord_Account");
        entity.HasOne<AuditRecord>().WithMany().HasForeignKey(row => row.AuditRecordId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountAuditRecord_AuditRecords");
    }
}
