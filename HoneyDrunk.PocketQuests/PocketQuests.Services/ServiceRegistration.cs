using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PocketQuests.Services.Quests;

namespace PocketQuests.Services;

/// <summary>Registers approved migrated operations with scoped persistence.</summary>
public static class ServiceRegistration
{
    /// <summary>Registers the completion exemplar and host clock.</summary>
    /// <param name="services">Host service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddQuestServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IQuestService, QuestService>();
        return services;
    }
}
