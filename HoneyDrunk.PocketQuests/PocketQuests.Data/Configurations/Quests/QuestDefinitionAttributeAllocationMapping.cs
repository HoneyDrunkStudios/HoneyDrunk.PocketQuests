using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Attributes;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestDefinitionAttributeAllocation fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestDefinitionAttributeAllocationMapping : IEntityTypeConfiguration<QuestDefinitionAttributeAllocationEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestDefinitionAttributeAllocationEntity> entity)
    {
        entity.ToTable("QuestDefinitionAttributeAllocation", "pocketquests", table =>
        {
            table.HasComment("One row is one attribute recipient in an immutable definition revision. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestDefinitionAttributeAllocation_BasisPoints", "[BasisPoints] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_QuestDefinitionAttributeAllocation_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinitionAttributeAllocation_Position", "[Position]>=0");
        });
        entity.HasKey(row => new { row.AccountId, row.QuestDefinitionRevisionId, row.AttributeId }).HasName("PK_QuestDefinitionAttributeAllocation").IsClustered();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionRevisionId).HasColumnType("uniqueidentifier").HasComment("Immutable revision that owns this allocation.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AttributeId).HasColumnType("varchar(40)").HasComment("Stable attribute receiving this share.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.BasisPoints).HasColumnType("int").HasComment("Exact share 0..10000; nonempty allocation pool must sum to 10000 in the controlled write.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Position).HasColumnType("int").HasComment("Original allocation array position, preserving the existing API order; v1 rows retain their display-order adapter.").IsRequired(true).HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => row.AttributeId, "IX_QuestDefinitionAttributeAllocation_FK_Attribute").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionAttributeAllocation_Account");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionAttributeAllocation_QuestDefinitionRevision");
        entity.HasOne<AttributeEntity>().WithMany().HasForeignKey(row => row.AttributeId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionAttributeAllocation_Attribute");
    }
}
