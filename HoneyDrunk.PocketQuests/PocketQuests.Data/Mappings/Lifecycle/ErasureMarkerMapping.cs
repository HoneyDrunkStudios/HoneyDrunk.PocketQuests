using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Lifecycle;

namespace PocketQuests.Data.Mappings.Lifecycle;

/// <summary>Maps ErasureMarker fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class ErasureMarkerMapping : IEntityTypeConfiguration<ErasureMarkerEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ErasureMarkerEntity> entity)
    {
        entity.ToTable("ErasureMarker", "pocketquests", table =>
        {
            table.HasComment("One row is one minimal verified deletion marker consisting only of opaque Identity ID and deletion time. Classification: Restricted. History: marker; Retain exactly 35 elapsed days after original verified live erasure, then remove. CreatedAt is the sole deletion/marker-creation clock and is preserved across restore and retry. External authoritative copies must survive backups and obey the same minimum-content policy.");
            table.HasCheckConstraint("CK_ErasureMarker_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_ErasureMarker_IdentityUserId", "DATALENGTH([Id])=30 AND LEFT([Id],4)='usr_' AND SUBSTRING([Id],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%' COLLATE Latin1_General_100_BIN2");
        });
        entity.HasKey(row => row.Id).HasName("PK_ErasureMarker").IsClustered();
        entity.Property(row => row.Id).HasColumnType("varchar(30)").HasComment("Canonical never-recycled Identity user ID used for restore fencing; intentionally no FK to deleted personal data.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Original verified live-erasure and marker-creation instant, written atomically with successful purge. The writer supplies this canonical value; restore/import must preserve it and duplicate delivery must not update it. Retention expires exactly 35 elapsed days after this instant, never after restore or retry. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.HasIndex(row => new { row.CreatedAt, row.Id }, "IX_ErasureMarker_Retention").IsUnique(false).HasFilter(null);
    }
}
