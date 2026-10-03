using HoneyDrunk.Audit.Data;
using HoneyDrunk.Data.EntityFramework.Context;
using HoneyDrunk.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities;

namespace PocketQuests.Data.Context;

/// <summary>Stores account-scoped quest events and command receipts through HoneyDrunk.Data.</summary>
public sealed class QuestDbContext(DbContextOptions<QuestDbContext> options) : HoneyDrunkDbContext(options)
{
    /// <summary>Gets the account identities and timezone settings.</summary>
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    /// <summary>Gets applied Identity lifecycle versions.</summary>
    public DbSet<LifecycleBarrierEntity> LifecycleBarriers => Set<LifecycleBarrierEntity>();

    /// <summary>Gets minimal verified-erasure markers.</summary>
    public DbSet<ErasureMarkerEntity> Erasures => Set<ErasureMarkerEntity>();

    /// <summary>Gets account-bound trusted offline clock anchors.</summary>
    public DbSet<SyncAnchorEntity> SyncAnchors => Set<SyncAnchorEntity>();

    /// <summary>Gets editable account-owned definitions.</summary>
    public DbSet<DefinitionEntity> Definitions => Set<DefinitionEntity>();

    /// <summary>Gets immutable custom definition history.</summary>
    public DbSet<DefinitionRevisionEntity> DefinitionRevisions => Set<DefinitionRevisionEntity>();

    /// <summary>Gets accepted occurrence snapshots.</summary>
    public DbSet<OccurrenceEntity> Occurrences => Set<OccurrenceEntity>();

    /// <summary>Gets append-only completion events.</summary>
    public DbSet<CompletionEntity> Completions => Set<CompletionEntity>();

    /// <summary>Gets append-only completion reversals.</summary>
    public DbSet<UndoEntity> Undos => Set<UndoEntity>();

    /// <summary>Gets durable idempotency receipts.</summary>
    public DbSet<OperationEntity> Operations => Set<OperationEntity>();

    /// <summary>Gets canonical audit evidence committed with each quest mutation.</summary>
    public DbSet<AuditRecord> Audit => Set<AuditRecord>();

    /// <inheritdoc />
    protected override void ApplyConfigurations(ModelBuilder b)
    {
        b.ApplyOutboxConfiguration();
        b.Entity<LifecycleBarrierEntity>().HasKey(x => x.UserId);
        b.Entity<LifecycleBarrierEntity>().Property(x => x.UserId).HasMaxLength(30).IsUnicode(false);
        b.Entity<ErasureMarkerEntity>().HasKey(x => x.UserId);
        b.Entity<ErasureMarkerEntity>().Property(x => x.UserId).HasMaxLength(30).IsUnicode(false);
        b.Entity<SyncAnchorEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<SyncAnchorEntity>().HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AccountEntity>().HasKey(x => x.Id);
        b.Entity<AccountEntity>().Property(x => x.IdentityKey).HasMaxLength(64).IsUnicode(false);
        b.Entity<AccountEntity>().HasIndex(x => x.IdentityKey).IsUnique();
        b.Entity<AccountEntity>().Property(x => x.Zone).HasMaxLength(100);
        b.Entity<DefinitionRevisionEntity>().HasKey(x => new { x.AccountId, x.DefinitionId, x.Revision });
        b.Entity<DefinitionRevisionEntity>().Property(x => x.DefinitionId).HasMaxLength(36).IsUnicode(false);
        b.Entity<DefinitionRevisionEntity>().HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DefinitionEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<DefinitionEntity>().Property(x => x.Id).HasMaxLength(36).IsUnicode(false);
        b.Entity<DefinitionEntity>().HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OccurrenceEntity>().Property(x => x.PlannedTime).HasMaxLength(5).IsUnicode(false);
        b.Entity<OccurrenceEntity>().HasOne<OccurrenceEntity>().WithMany().HasForeignKey(x => new { x.AccountId, x.ParentId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OccurrenceEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<OccurrenceEntity>().Property(x => x.DueDate).HasMaxLength(10).IsUnicode(false);
        b.Entity<OccurrenceEntity>().HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CompletionEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<CompletionEntity>().HasOne<OccurrenceEntity>().WithMany().HasForeignKey(x => new { x.AccountId, x.OccurrenceId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UndoEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<UndoEntity>().HasIndex(x => new { x.AccountId, x.CompletionId }).IsUnique();
        b.Entity<UndoEntity>().HasOne<CompletionEntity>().WithMany().HasForeignKey(x => new { x.AccountId, x.CompletionId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OperationEntity>().HasKey(x => new { x.AccountId, x.Id });
        b.Entity<OperationEntity>().HasOne<AccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditRecord>().ToTable("AuditRecords");
        b.Entity<AuditRecord>().HasKey(x => x.Id);
        b.Entity<AuditRecord>().Property(x => x.Id).HasMaxLength(32).IsUnicode(false);
        b.Entity<AuditRecord>().Property(x => x.EventName).HasMaxLength(200);
        b.Entity<AuditRecord>().Property(x => x.TenantId).HasMaxLength(100);
        b.Entity<AuditRecord>().HasIndex(x => new { x.TenantId, x.OccurredAt });
    }
}
