using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Immutable system template revision and same-identity pointer constraints.</summary>
public sealed class SystemQuestRevisionMapping : IEntityTypeConfiguration<SystemQuestRevisionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SystemQuestRevisionEntity> entity)
    {
        entity.ToTable("SystemQuestRevision", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable operator-authored public system-quest template revision. Classification: Public. History: insert-only; the row is the content history, while shared Audit records the actor separately. Retain referenced public content; at most 1000 revisions per authored identity and 200 authored identities.");
            table.HasCheckConstraint("CK_SystemQuestRevision_Version", "[Version] BETWEEN 1 AND 1000");
            table.HasCheckConstraint("CK_SystemQuestRevision_SnapshotJson", "ISJSON([SnapshotJson])=1 AND DATALENGTH([SnapshotJson])<=32768");
            table.HasCheckConstraint("CK_SystemQuestRevision_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_SystemQuestRevision").IsClustered();
        entity.Property(row => row.Id).HasComment("Application-generated stable identity of this immutable template revision.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.SystemQuestId).HasComment("Stable system quest identity owning this revision; never reused.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Version).HasComment("Monotonic content revision from 1 through 1000 within this system quest.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.SnapshotJson).HasComment("Frozen complete quest terms under ruleset 1.0. Display and replay only, never queried as relational fields; valid JSON at most 32768 UTF-16 bytes.").UseCollation("Latin1_General_100_BIN2").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Trusted UTC insertion instant of this immutable content revision.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasAlternateKey(row => new { row.SystemQuestId, row.Id }).HasName("UQ_SystemQuestRevision_SystemQuestId_Id");
        entity.HasAlternateKey(row => new { row.SystemQuestId, row.Version }).HasName("UQ_SystemQuestRevision_SystemQuestId_Version");
        entity.HasOne<SystemQuestEntity>().WithMany().HasForeignKey(row => row.SystemQuestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemQuestRevision_SystemQuest");
    }
}
