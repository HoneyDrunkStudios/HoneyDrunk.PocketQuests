using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities;

namespace PocketQuests.Data.Configurations;

/// <summary>Fluent mapping for the SyncAnchors table.</summary>
public sealed class SyncAnchorEntityConfiguration : IEntityTypeConfiguration<SyncAnchorEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SyncAnchorEntity> builder)
    {
        builder.ToTable("SyncAnchors", "dbo");
        builder.HasKey(x => new { x.AccountId, x.Id });
        builder.HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
