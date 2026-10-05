using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PocketQuests.Api.Hosting;

/// <summary>Configures JSON binding and serialization for the public API.</summary>
public static class ApiJsonConfiguration
{
    /// <summary>Registers enum serialization and rejects missing or null required request values as malformed input.</summary>
    /// <param name="services">The API service collection.</param>
    public static void AddApiJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    info =>
                    {
                        if (info.Type.Namespace?.StartsWith("PocketQuests.Contracts.Models.", StringComparison.Ordinal) == true
                            || info.Type.Namespace?.StartsWith("PocketQuests.Contracts.Responses.", StringComparison.Ordinal) == true)
                            info.NumberHandling = JsonNumberHandling.Strict;
                    },
                },
            });
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
        });
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
    }
}
