using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Kernel.Hosting;
using HoneyDrunk.Kernel.Telemetry;
using HoneyDrunk.Telemetry.OpenTelemetry.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PocketQuests.ServiceDefaults;

/// <summary>Shared Kernel context, Pulse telemetry and health registration for the API.</summary>
public static class Extensions
{
    /// <summary>Registers the required shared node runtime and telemetry exporters.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The application builder.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddHoneyDrunkNode(options =>
        {
            options.NodeId = new NodeId("pocket-quests");
            options.SectorId = new SectorId("apps");
            options.EnvironmentId = new EnvironmentId(builder.Environment.EnvironmentName.ToLowerInvariant());
            options.StudioId = "honeydrunk-studios";
        }).AddTelemetry();

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddHoneyDrunkOpenTelemetry(options =>
            {
                options.ServiceName = "pocket-quests";
                options.Environment = builder.Environment.EnvironmentName;
                options.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
                options.AdditionalActivitySources.Add("HoneyDrunk.Data");
            });
        }

        return builder;
    }

    /// <summary>Maps readiness and liveness endpoints.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application with health routes registered.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = _ => false });
        return app;
    }
}
