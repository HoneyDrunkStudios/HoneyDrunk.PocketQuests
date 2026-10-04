using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PocketQuests.Domain.Services.Accounts;
using PocketQuests.Domain.Services.Attributes;
using PocketQuests.Domain.Services.Categories;
using PocketQuests.Domain.Services.Lifecycle;
using PocketQuests.Domain.Services.Progress;
using PocketQuests.Domain.Services.Quests;
using PocketQuests.Domain.Services.Skills;
using PocketQuests.Domain.Services.Synchronization;

namespace PocketQuests.Domain.Services;

/// <summary>Registers scoped business services over the scoped Data layer.</summary>
public static class ServiceRegistration
{
    /// <summary>Registers each product entity's business service.</summary>
    /// <param name="services">Host service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddQuestBusinessServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAttributeService, AttributeService>();
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<IProfileRewardService, ProfileRewardService>();
        services.AddScoped<ISystemQuestService, SystemQuestService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICommandReceiptService, CommandReceiptService>();
        services.AddScoped<IAccountInterestService, AccountInterestService>();
        services.AddScoped<IAccountPauseService, AccountPauseService>();
        services.AddScoped<ITimeZoneChangeService, TimeZoneChangeService>();
        services.AddScoped<ICustomSkillService, CustomSkillService>();
        services.AddScoped<ISkillAssessmentService, SkillAssessmentService>();
        services.AddScoped<IQuestDefinitionService, QuestDefinitionService>();
        services.AddScoped<IQuestDefinitionRevisionService, QuestDefinitionRevisionService>();
        services.AddScoped<IQuestDefinitionAttributeAllocationService, QuestDefinitionAttributeAllocationService>();
        services.AddScoped<IQuestDefinitionSkillAllocationService, QuestDefinitionSkillAllocationService>();
        services.AddScoped<IQuestSeriesService, QuestSeriesService>();
        services.AddScoped<IQuestSeriesRevisionService, QuestSeriesRevisionService>();
        services.AddScoped<ISyncAnchorService, SyncAnchorService>();
        services.AddScoped<IQuestOccurrenceService, QuestOccurrenceService>();
        services.AddScoped<IQuestOccurrenceRevisionService, QuestOccurrenceRevisionService>();
        services.AddScoped<IQuestOccurrenceEventService, QuestOccurrenceEventService>();
        services.AddScoped<IQuestCompletionService, QuestCompletionService>();
        services.AddScoped<IXpLedgerEntryService, XpLedgerEntryService>();
        services.AddScoped<IXpBalanceService, XpBalanceService>();
        services.AddScoped<ICategoryProgressService, CategoryProgressService>();
        services.AddScoped<IAccountEntitlementService, AccountEntitlementService>();
        services.AddScoped<IAccountLifecycleStateService, AccountLifecycleStateService>();
        services.AddScoped<IErasureMarkerService, ErasureMarkerService>();
        services.AddScoped<ILifecycleMessageService, LifecycleMessageService>();
        services.AddScoped<IAccountAuditRecordService, AccountAuditRecordService>();
        services.AddScoped<IQuestCommandHistoryService, QuestCommandHistoryService>();
        services.AddScoped<IQuestCommandInterestService, QuestCommandInterestService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IQuestLifecycle>(provider => provider.GetRequiredService<IAccountLifecycleStateService>());
        return services;
    }
}
