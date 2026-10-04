using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Mappings.Quests;

/// <summary>Maps QuestDefinitionSkillAllocation fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestDefinitionSkillAllocationMapping : IEntityTypeConfiguration<QuestDefinitionSkillAllocationEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestDefinitionSkillAllocationEntity> entity)
    {
        entity.ToTable("QuestDefinitionSkillAllocation", "pocketquests", table =>
        {
            table.HasComment("One row is one system/custom skill recipient in an immutable definition revision. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_ExactlyOneSkill", "([SystemSkillId] IS NOT NULL AND [CustomSkillId] IS NULL) OR ([SystemSkillId] IS NULL AND [CustomSkillId] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_BasisPoints", "[BasisPoints] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_Position", "[Position]>=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestDefinitionSkillAllocation").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_QuestDefinitionSkillAllocation_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionRevisionId, row.SystemSkillId, row.CustomSkillId }, "UQ_QuestDefinitionSkillAllocation_RevisionSkill").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestDefinitionRevisionId).HasColumnType("uniqueidentifier").HasComment("Immutable revision that owns this allocation.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.SystemSkillId).HasColumnType("varchar(40)").HasComment("System skill being referenced; null when CustomSkillId is supplied.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CustomSkillId).HasColumnType("uniqueidentifier").HasComment("Custom skill owned by this account; null when SystemSkillId is supplied.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.BasisPoints).HasColumnType("int").HasComment("Exact share 0..10000; nonempty allocation pool must sum to 10000 in the controlled write.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.Position).HasColumnType("int").HasComment("Original allocation array position, preserving the existing API order; v1 rows retain their display-order adapter.").IsRequired(true).HasDefaultValueSql("0");
        entity.HasIndex(row => row.SystemSkillId, "IX_QuestDefinitionSkillAllocation_FK_Skill_System").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_QuestDefinitionSkillAllocation_FK_CustomSkill").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_Account");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_QuestDefinitionRevision");
        entity.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_Skill_System");
        entity.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_CustomSkill");
    }
}
