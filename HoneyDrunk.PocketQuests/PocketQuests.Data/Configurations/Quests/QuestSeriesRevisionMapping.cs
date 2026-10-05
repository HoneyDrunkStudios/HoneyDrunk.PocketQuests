using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestSeriesRevision fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestSeriesRevisionMapping : IEntityTypeConfiguration<QuestSeriesRevisionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestSeriesRevisionEntity> entity)
    {
        entity.ToTable("QuestSeriesRevision", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable recurrence configuration and consent state. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestSeriesRevision_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestSeriesRevision_CadenceCode", "[CadenceCode] IN ('Days','Weeks','Months','Years')");
            table.HasCheckConstraint("CK_QuestSeriesRevision_Schedule", "[Interval] BETWEEN 1 AND 999 AND [AnchorOn] < CONVERT(date,'9999-01-01')");
            table.HasCheckConstraint("CK_QuestSeriesRevision_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_QuestSeriesRevision_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestSeriesRevision_ScheduleVersion", "[ScheduleVersion]>=1");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestSeriesRevision").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_QuestSeriesRevision_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId, row.Revision }, "UQ_QuestSeriesRevision_AccountId_QuestSeriesId_Revision").IsUnique().HasFilter(null);
        entity.HasAlternateKey(row => new { row.AccountId, row.QuestSeriesId, row.Id }).HasName("UQ_QuestSeriesRevision_AccountId_QuestSeriesId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestSeriesId).HasComment("Series to which this version belongs.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Revision).HasComment("Positive configuration sequence within a series.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionRevisionId).HasComment("Frozen terms scheduled by this version.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CadenceCode).HasComment("Calendar recurrence unit: Days, Weeks, Months or Years.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(6).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Interval).HasComment("Calendar-unit interval 1..999, matching current validation.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AnchorOn).HasComment("First delivery date in the selected account calendar; must precede year 9999.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.PlannedTime).HasColumnType("time(0)").HasComment("Optional local planned clock time, not the completion deadline. Null means no planned time.").HasPrecision(0).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.HasAutoAcceptPenalty).HasComment("Explicit consent to automatically accept this version of recurring penalty terms.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EffectiveAt).HasComment("When this configuration became effective. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandReceiptId).HasComment("Receipt for the command that caused this fact.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ScheduleVersion).HasComment("Public domain schedule version; internal revision also changes when definition terms change without editing recurrence.").HasDefaultValueSql("1").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionRevisionId }, "IX_QuestSeriesRevision_FK_QuestDefinitionRevision");
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestSeriesRevision_FK_CommandReceipt");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeriesRevision_Account");
        entity.HasOne<QuestSeriesEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestSeriesId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeriesRevision_QuestSeries");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeriesRevision_QuestDefinitionRevision");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeriesRevision_CommandReceipt");
    }
}
