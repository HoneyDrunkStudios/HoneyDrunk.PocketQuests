using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestSeries fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestSeriesMapping : IEntityTypeConfiguration<QuestSeriesEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestSeriesEntity> entity)
    {
        entity.ToTable("QuestSeries", "pocketquests", table =>
        {
            table.HasComment("One row is one opted-in recurrence series and its durable delivery cursor. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestSeries_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestSeries_NextSequence", "[NextSequence] >= 0");
            table.HasCheckConstraint("CK_QuestSeries_PauseDays", "[PauseDays] >= 0");
            table.HasCheckConstraint("CK_QuestSeries_StoppedAtUtc", "[StoppedAt] IS NULL OR DATEPART(TZOFFSET,[StoppedAt])=0");
            table.HasCheckConstraint("CK_QuestSeries_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestSeries_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_QuestSeries_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_QuestSeries_CreationOrdinal", "[CreationOrdinal]>=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestSeries").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestSeries_AccountId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionId).HasComment("Quest identity delivered by this recurrence series.");
        entity.Property(row => row.Revision).HasComment("Current immutable series configuration revision.");
        entity.Property(row => row.NextSequence).HasComment("Next nonnegative sequence within the current series revision.");
        entity.Property(row => row.PauseDays).HasComment("Nonnegative local-calendar days shifted by effective pauses.");
        entity.Property(row => row.NextDeliveryOn).HasComment("Next delivery date in the selected account calendar; null when stopped or no representable next date.");
        entity.Property(row => row.StoppedAt).HasComment("Time recurrence was explicitly stopped; stopped series are never silently restarted. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.NextDeliveryOn, row.Id }, "IX_QuestSeries_Due").IsUnique(false).HasFilter("[StoppedAt] IS NULL");
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId }, "IX_QuestSeries_FK_QuestDefinition");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeries_Account");
        entity.HasOne<QuestDefinitionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeries_QuestDefinition");
    }
}
