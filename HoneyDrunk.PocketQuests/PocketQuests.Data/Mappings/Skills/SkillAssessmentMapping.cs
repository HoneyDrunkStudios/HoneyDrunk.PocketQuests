using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Mappings.Skills;

/// <summary>Maps SkillAssessment fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class SkillAssessmentMapping : IEntityTypeConfiguration<SkillAssessmentEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SkillAssessmentEntity> entity)
    {
        entity.ToTable("SkillAssessment", "pocketquests", table =>
        {
            table.HasComment("One row is one replacement experience placement effective at a recorded time. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_SkillAssessment_ExactlyOneSkill", "([SystemSkillId] IS NOT NULL AND [CustomSkillId] IS NULL) OR ([SystemSkillId] IS NULL AND [CustomSkillId] IS NOT NULL)");
            table.HasCheckConstraint("CK_SkillAssessment_ExperienceCode", "[ExperienceCode] IN ('New','Practiced','Experienced','Expert')");
            table.HasCheckConstraint("CK_SkillAssessment_SeedXp", "[SeedXp] >= 0");
            table.HasCheckConstraint("CK_SkillAssessment_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_SkillAssessment_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_SkillAssessment").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_SkillAssessment_AccountId_Id").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.SystemSkillId).HasColumnType("varchar(40)").HasComment("System skill being referenced; null when CustomSkillId is supplied.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CustomSkillId).HasColumnType("uniqueidentifier").HasComment("Custom skill owned by this account; null when SystemSkillId is supplied.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ExperienceCode).HasColumnType("varchar(12)").HasComment("New, Practiced, Experienced or Expert placement; this is not earned XP.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.SeedXp).HasColumnType("bigint").HasComment("Exact placement component under RulesetVersion; replaces the prior seed without granting other pools.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RulesetVersion).HasColumnType("varchar(32)").HasComment("Rules version that maps experience placement to seed XP.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.EffectiveAt).HasColumnType("datetimeoffset(7)").HasComment("Recorded placement time. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CommandReceiptId).HasColumnType("uniqueidentifier").HasComment("Receipt for the command that caused this fact.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => new { row.AccountId, row.SystemSkillId, row.CustomSkillId, row.EffectiveAt, row.Id }, "IX_SkillAssessment_SkillTimeline").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.SystemSkillId, "IX_SkillAssessment_FK_Skill_System").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_SkillAssessment_FK_CustomSkill").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_SkillAssessment_FK_CommandReceipt").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SkillAssessment_Account");
        entity.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SkillAssessment_Skill_System");
        entity.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SkillAssessment_CustomSkill");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SkillAssessment_CommandReceipt");
    }
}
