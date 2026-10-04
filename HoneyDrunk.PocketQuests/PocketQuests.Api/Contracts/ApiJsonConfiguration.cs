using System.Text.Json.Serialization;

namespace PocketQuests.Api.Contracts;

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
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
        });
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
    }
}
