using HoneyDrunk.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Configurations;
using PocketQuests.Data.Configurations.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Attributes;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data;

/// <summary>Tracks product entities and shared Audit/Outbox rows in the same EF transaction.</summary>
/// <param name="options">SQL connection and EF options.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : BaseDbContext(options)
{
    /// <summary>Gets Category rows.</summary>
    public DbSet<CategoryEntity> Category => Set<CategoryEntity>();

    /// <summary>Gets Attribute rows.</summary>
    public DbSet<AttributeEntity> Attribute => Set<AttributeEntity>();

    /// <summary>Gets Skill rows.</summary>
    public DbSet<SkillEntity> Skill => Set<SkillEntity>();

    /// <summary>Gets ProfileReward rows.</summary>
    public DbSet<ProfileRewardEntity> ProfileReward => Set<ProfileRewardEntity>();

    /// <summary>Gets SystemQuest rows.</summary>
    public DbSet<SystemQuestEntity> SystemQuest => Set<SystemQuestEntity>();

    /// <summary>Gets Account rows.</summary>
    public DbSet<AccountEntity> Account => Set<AccountEntity>();

    /// <summary>Gets CommandReceipt rows.</summary>
    public DbSet<CommandReceiptEntity> CommandReceipt => Set<CommandReceiptEntity>();

    /// <summary>Gets AccountInterest rows.</summary>
    public DbSet<AccountInterestEntity> AccountInterest => Set<AccountInterestEntity>();

    /// <summary>Gets AccountPause rows.</summary>
    public DbSet<AccountPauseEntity> AccountPause => Set<AccountPauseEntity>();

    /// <summary>Gets TimeZoneChange rows.</summary>
    public DbSet<TimeZoneChangeEntity> TimeZoneChange => Set<TimeZoneChangeEntity>();

    /// <summary>Gets CustomSkill rows.</summary>
    public DbSet<CustomSkillEntity> CustomSkill => Set<CustomSkillEntity>();

    /// <summary>Gets SkillAssessment rows.</summary>
    public DbSet<SkillAssessmentEntity> SkillAssessment => Set<SkillAssessmentEntity>();

    /// <summary>Gets QuestDefinition rows.</summary>
    public DbSet<QuestDefinitionEntity> QuestDefinition => Set<QuestDefinitionEntity>();

    /// <summary>Gets QuestDefinitionRevision rows.</summary>
    public DbSet<QuestDefinitionRevisionEntity> QuestDefinitionRevision => Set<QuestDefinitionRevisionEntity>();

    /// <summary>Gets QuestDefinitionAttributeAllocation rows.</summary>
    public DbSet<QuestDefinitionAttributeAllocationEntity> QuestDefinitionAttributeAllocation => Set<QuestDefinitionAttributeAllocationEntity>();

    /// <summary>Gets QuestDefinitionSkillAllocation rows.</summary>
    public DbSet<QuestDefinitionSkillAllocationEntity> QuestDefinitionSkillAllocation => Set<QuestDefinitionSkillAllocationEntity>();

    /// <summary>Gets QuestSeries rows.</summary>
    public DbSet<QuestSeriesEntity> QuestSeries => Set<QuestSeriesEntity>();

    /// <summary>Gets QuestSeriesRevision rows.</summary>
    public DbSet<QuestSeriesRevisionEntity> QuestSeriesRevision => Set<QuestSeriesRevisionEntity>();

    /// <summary>Gets SyncAnchor rows.</summary>
    public DbSet<SyncAnchorEntity> SyncAnchor => Set<SyncAnchorEntity>();

    /// <summary>Gets QuestOccurrence rows.</summary>
    public DbSet<QuestOccurrenceEntity> QuestOccurrence => Set<QuestOccurrenceEntity>();

    /// <summary>Gets QuestOccurrenceRevision rows.</summary>
    public DbSet<QuestOccurrenceRevisionEntity> QuestOccurrenceRevision => Set<QuestOccurrenceRevisionEntity>();

    /// <summary>Gets QuestOccurrenceEvent rows.</summary>
    public DbSet<QuestOccurrenceEventEntity> QuestOccurrenceEvent => Set<QuestOccurrenceEventEntity>();

    /// <summary>Gets QuestCompletion rows.</summary>
    public DbSet<QuestCompletionEntity> QuestCompletion => Set<QuestCompletionEntity>();

    /// <summary>Gets XpLedgerEntry rows.</summary>
    public DbSet<XpLedgerEntryEntity> XpLedgerEntry => Set<XpLedgerEntryEntity>();

    /// <summary>Gets XpBalance rows.</summary>
    public DbSet<XpBalanceEntity> XpBalance => Set<XpBalanceEntity>();

    /// <summary>Gets CategoryProgress rows.</summary>
    public DbSet<CategoryProgressEntity> CategoryProgress => Set<CategoryProgressEntity>();

    /// <summary>Gets AccountEntitlement rows.</summary>
    public DbSet<AccountEntitlementEntity> AccountEntitlement => Set<AccountEntitlementEntity>();

    /// <summary>Gets AccountLifecycleState rows.</summary>
    public DbSet<AccountLifecycleStateEntity> AccountLifecycleState => Set<AccountLifecycleStateEntity>();

    /// <summary>Gets ErasureMarker rows.</summary>
    public DbSet<ErasureMarkerEntity> ErasureMarker => Set<ErasureMarkerEntity>();

    /// <summary>Gets LifecycleMessage rows.</summary>
    public DbSet<LifecycleMessageEntity> LifecycleMessage => Set<LifecycleMessageEntity>();

    /// <summary>Gets AccountAuditRecord rows.</summary>
    public DbSet<AccountAuditRecordEntity> AccountAuditRecord => Set<AccountAuditRecordEntity>();

    /// <summary>Gets QuestCommandHistory rows.</summary>
    public DbSet<QuestCommandHistoryEntity> QuestCommandHistory => Set<QuestCommandHistoryEntity>();

    /// <summary>Gets QuestCommandInterest rows.</summary>
    public DbSet<QuestCommandInterestEntity> QuestCommandInterest => Set<QuestCommandInterestEntity>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }

    /// <inheritdoc />
    protected override void ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountMapping).Assembly);
        modelBuilder.ApplyOutboxConfiguration();
    }
}
