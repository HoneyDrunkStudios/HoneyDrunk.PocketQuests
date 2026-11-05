using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Discoveries;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Discoveries;

/// <summary>Maps private saved places to the reviewed DACPAC schema.</summary>
public sealed class SavedPlaceMapping : IEntityTypeConfiguration<SavedPlaceEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SavedPlaceEntity> entity)
    {
        entity.ToTable("SavedPlace", "pocketquests", table =>
        {
            table.HasComment("One row is one private place saved by an account. Classification: Restricted. History: mutable current record with creation/modification stamps and rowversion; action audit contains no notes or coordinates. At most 100 per account enforced under the account lock; delete on removal or final account erasure. No old note copies or location tracking.");
            table.HasCheckConstraint("CK_SavedPlace_Coordinates", "[Latitude] BETWEEN -85 AND 85 AND [Longitude] BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_SavedPlace_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_SavedPlace_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_SavedPlace_PlaceKey", "([PlaceKey] LIKE 'node/[1-9]%' AND SUBSTRING([PlaceKey],6,80) NOT LIKE '%[^0-9]%') OR ([PlaceKey] LIKE 'way/[1-9]%' AND SUBSTRING([PlaceKey],5,80) NOT LIKE '%[^0-9]%') OR ([PlaceKey] LIKE 'relation/[1-9]%' AND SUBSTRING([PlaceKey],10,80) NOT LIKE '%[^0-9]%')");
            table.HasCheckConstraint("CK_SavedPlace_KindCategory", "([Kind] IN ('Parks','Gardens') AND [CategoryId]='c07') OR ([Kind] IN ('Libraries','Museums') AND [CategoryId]='c02') OR ([Kind]='Art' AND [CategoryId]='c03') OR ([Kind]='Community' AND [CategoryId]='c05') OR ([Kind]='Markets' AND [CategoryId]='c06') OR ([Kind]='Viewpoints' AND [CategoryId]='c10')");
            table.HasCheckConstraint("CK_SavedPlace_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_SavedPlace_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_SavedPlace_VisitedAtUtc", "[VisitedAt] IS NULL OR DATEPART(TZOFFSET,[VisitedAt])=0");
            table.HasCheckConstraint("CK_SavedPlace_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_SavedPlace").IsClustered();
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Id).HasComment("Application-generated stable identifier for this saved place.");
        entity.Property(row => row.AccountId).HasComment("Product account that owns the private discovery; supplied only by trusted server context.");
        entity.Property(row => row.PlaceKey).HasComment("OpenStreetMap element key, node/number, way/number or relation/number; external identifier, not a product foreign key.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(false);
        entity.Property(row => row.Name).HasComment("Player-visible place name captured when saved; not an assurance of current provider accuracy.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(180);
        entity.Property(row => row.Kind).HasComment("Supported place category label: Parks, Gardens, Libraries, Museums, Art, Community, Markets or Viewpoints.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(24).IsUnicode(false);
        entity.Property(row => row.CategoryId).HasComment("Quest category associated with this type of place; grants no progress.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.Latitude).HasComment("Saved place latitude in degrees, between minus 85 and 85; not a player movement history.").HasPrecision(9, 6);
        entity.Property(row => row.Longitude).HasComment("Saved place longitude in degrees, between minus 180 and 180.").HasPrecision(9, 6);
        entity.Property(row => row.Notes).HasComment("Private player-authored discovery notes; empty string means no notes.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(1000);
        entity.Property(row => row.VisitedAt).HasComment("Server UTC instant when the player marked this place visited; null means want to visit. Self-report, not GPS proof.").HasPrecision(7);
        entity.Property(row => row.QuestOccurrenceId).HasComment("Optional accepted quest owned by the same account; null means no linked quest. Completion remains explicit.");
        entity.Property(row => row.Revision).HasComment("Positive optimistic edit revision; stale writes must reload rather than replace newer edits.");
        entity.Property(row => row.CreatedAt).HasComment("Server UTC instant when this saved-place record was inserted.").HasPrecision(7);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC instant of the most recent saved-place edit; never before CreatedAt.").HasPrecision(7);
        entity.Property(row => row.RowVersion).HasComment("SQL Server optimistic concurrency token; not a timestamp.").IsRowVersion();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_SavedPlace_AccountId_Id");
        entity.HasAlternateKey(row => new { row.AccountId, row.PlaceKey }).HasName("UQ_SavedPlace_AccountId_PlaceKey");
        entity.HasIndex(row => new { row.CategoryId }, "IX_SavedPlace_CategoryId");
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId }, "IX_SavedPlace_AccountId_QuestOccurrenceId");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => new { row.AccountId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SavedPlace_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => new { row.CategoryId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SavedPlace_Category");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SavedPlace_QuestOccurrence");
    }
}
