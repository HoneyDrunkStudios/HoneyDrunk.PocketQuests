using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Attributes;
using PocketQuests.Data.DataServices.Categories;
using PocketQuests.Data.DataServices.Lifecycle;
using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.DataServices.Synchronization;

namespace PocketQuests.Data.DataServices;

/// <summary>Registers one EF context and explicit per-entity data services for each application scope.</summary>
public static class DataServiceRegistration
{
    /// <summary>Registers canonical product persistence without publishing or migrating the schema.</summary>
    /// <param name="services">Host services.</param>
    /// <param name="connectionString">Configured product SQL connection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddQuestDataServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<BaseDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped(typeof(IBaseDataService<>), typeof(BaseDataService<>));
        services.AddScoped<ICategoryDataService, CategoryDataService>();
        services.AddScoped<IAttributeDataService, AttributeDataService>();
        services.AddScoped<ISkillDataService, SkillDataService>();
        services.AddScoped<IProfileRewardDataService, ProfileRewardDataService>();
        services.AddScoped<ISystemQuestDataService, SystemQuestDataService>();
        services.AddScoped<IAccountDataService, AccountDataService>();
        services.AddScoped<ICommandReceiptDataService, CommandReceiptDataService>();
        services.AddScoped<IAccountInterestDataService, AccountInterestDataService>();
        services.AddScoped<IAccountPauseDataService, AccountPauseDataService>();
        services.AddScoped<ITimeZoneChangeDataService, TimeZoneChangeDataService>();
        services.AddScoped<ICustomSkillDataService, CustomSkillDataService>();
        services.AddScoped<ISkillAssessmentDataService, SkillAssessmentDataService>();
        services.AddScoped<IQuestDefinitionDataService, QuestDefinitionDataService>();
        services.AddScoped<IQuestDefinitionRevisionDataService, QuestDefinitionRevisionDataService>();
        services.AddScoped<IQuestDefinitionAttributeAllocationDataService, QuestDefinitionAttributeAllocationDataService>();
        services.AddScoped<IQuestDefinitionSkillAllocationDataService, QuestDefinitionSkillAllocationDataService>();
        services.AddScoped<IQuestSeriesDataService, QuestSeriesDataService>();
        services.AddScoped<IQuestSeriesRevisionDataService, QuestSeriesRevisionDataService>();
        services.AddScoped<ISyncAnchorDataService, SyncAnchorDataService>();
        services.AddScoped<IQuestOccurrenceDataService, QuestOccurrenceDataService>();
        services.AddScoped<IQuestOccurrenceRevisionDataService, QuestOccurrenceRevisionDataService>();
        services.AddScoped<IQuestOccurrenceEventDataService, QuestOccurrenceEventDataService>();
        services.AddScoped<IQuestCompletionDataService, QuestCompletionDataService>();
        services.AddScoped<IXpLedgerEntryDataService, XpLedgerEntryDataService>();
        services.AddScoped<IXpBalanceDataService, XpBalanceDataService>();
        services.AddScoped<ICategoryProgressDataService, CategoryProgressDataService>();
        services.AddScoped<IAccountEntitlementDataService, AccountEntitlementDataService>();
        services.AddScoped<IAccountLifecycleStateDataService, AccountLifecycleStateDataService>();
        services.AddScoped<IErasureMarkerDataService, ErasureMarkerDataService>();
        services.AddScoped<ILifecycleMessageDataService, LifecycleMessageDataService>();
        services.AddScoped<IAccountAuditRecordDataService, AccountAuditRecordDataService>();
        services.AddScoped<IQuestCommandHistoryDataService, QuestCommandHistoryDataService>();
        services.AddScoped<IQuestCommandInterestDataService, QuestCommandInterestDataService>();
        return services;
    }
}
