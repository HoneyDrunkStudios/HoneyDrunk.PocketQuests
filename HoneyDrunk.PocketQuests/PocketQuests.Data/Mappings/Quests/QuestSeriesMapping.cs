using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Mappings.Quests;

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
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestDefinitionId).HasColumnType("uniqueidentifier").HasComment("Quest identity delivered by this recurrence series.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Revision).HasColumnType("int").HasComment("Current immutable series configuration revision.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.NextSequence).HasColumnType("int").HasComment("Next nonnegative sequence within the current series revision.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.PauseDays).HasColumnType("int").HasComment("Nonnegative local-calendar days shifted by effective pauses.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.NextDeliveryOn).HasColumnType("date").HasComment("Next delivery date in the selected account calendar; null when stopped or no representable next date.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.StoppedAt).HasColumnType("datetimeoffset(7)").HasComment("Time recurrence was explicitly stopped; stopped series are never silently restarted. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRequired(true).IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasColumnType("int").HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").IsRequired(true).HasDefaultValueSql("0");
        entity.HasIndex(row => new { row.AccountId, row.NextDeliveryOn, row.Id }, "IX_QuestSeries_Due").IsUnique(false).HasFilter("[StoppedAt] IS NULL");
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId }, "IX_QuestSeries_FK_QuestDefinition").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeries_Account");
        entity.HasOne<QuestDefinitionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestSeries_QuestDefinition");
    }
}
