using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestDefinitionSkillAllocation fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestDefinitionSkillAllocationMapping : IEntityTypeConfiguration<QuestDefinitionSkillAllocationEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestDefinitionSkillAllocationEntity> builder)
    {
        builder.ToTable("QuestDefinitionSkillAllocation", "pocketquests", table =>
        {
            table.HasComment("One row is one system/custom skill recipient in an immutable definition revision. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_ExactlyOneSkill", "([SystemSkillId] IS NOT NULL AND [CustomSkillId] IS NULL) OR ([SystemSkillId] IS NULL AND [CustomSkillId] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_BasisPoints", "[BasisPoints] BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinitionSkillAllocation_Position", "[Position]>=0");
        });
        builder.HasKey(row => row.Id).HasName("PK_QuestDefinitionSkillAllocation").IsClustered();
        builder.HasIndex(row => new { row.AccountId, row.Id }, "UQ_QuestDefinitionSkillAllocation_AccountId_Id").IsUnique().HasFilter(null);
        builder.HasIndex(row => new { row.AccountId, row.QuestDefinitionRevisionId, row.SystemSkillId, row.CustomSkillId }, "UQ_QuestDefinitionSkillAllocation_RevisionSkill").IsUnique().HasFilter(null);
        builder.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestDefinitionRevisionId).HasComment("Immutable revision that owns this allocation.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.SystemSkillId).HasComment("System skill being referenced; null when CustomSkillId is supplied.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CustomSkillId).HasComment("Custom skill owned by this account; null when SystemSkillId is supplied.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.BasisPoints).HasComment("Exact share 0..10000; nonempty allocation pool must sum to 10000 in the controlled write.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.Position).HasComment("Original allocation array position, preserving the existing API order; v1 rows retain their display-order adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(row => row.SystemSkillId, "IX_QuestDefinitionSkillAllocation_FK_Skill_System");
        builder.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_QuestDefinitionSkillAllocation_FK_CustomSkill");
        builder.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_Account");
        builder.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_QuestDefinitionRevision");
        builder.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_Skill_System");
        builder.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionSkillAllocation_CustomSkill");
    }
}
