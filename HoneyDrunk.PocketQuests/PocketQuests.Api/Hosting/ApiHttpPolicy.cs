using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace PocketQuests.Api.Hosting;

/// <summary>Registers the standard ASP.NET Core HTTP protections.</summary>
public static class ApiHttpPolicy
{
    /// <summary>The account command budget.</summary>
    public const string Commands = "commands";

    /// <summary>The separate account export budget.</summary>
    public const string Exports = "exports";

    /// <summary>Configures HTTP policies with environment-sensitive defaults.</summary>
    /// <param name="builder">The host being configured.</param>
    public static void AddApiHttpPolicy(this WebApplicationBuilder builder)
    {
        var local = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
        var settings = new ApiHttpOptions();
        if (local && !builder.Configuration.GetSection("Api:Http:AllowedOrigins").Exists())
            settings.AllowedOrigins.AddRange(["http://localhost:8081", "http://127.0.0.1:8081"]);
        builder.Configuration.GetSection("Api:Http").Bind(settings);
        if (settings.CommandPermitLimit < 1 || settings.ExportPermitLimit < 1 || settings.RateWindowSeconds is < 1 or > 86400
            || settings.HttpsPort is < 1 or > 65535
            || settings.KnownProxies.Any(value => !IPAddress.TryParse(value, out _))
            || settings.AllowedOrigins.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var origin)
                || origin.Scheme is not ("http" or "https") || origin.GetLeftPart(UriPartial.Authority) != value
                || (!local && origin.Scheme != "https")))
            throw new OptionsValidationException("Api:Http", typeof(ApiHttpOptions), ["Configure positive rate limits, valid proxy IPs and exact browser origins (HTTPS outside development)."]);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var value in settings.KnownProxies)
                options.KnownProxies.Add(IPAddress.Parse(value));

            // Empty trust lists must never mean trust every caller, including when the hosting env enables forwarding.
            options.ForwardedHeaders = settings.KnownProxies.Count == 0 ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
        });
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = settings.HttpsPort);
        builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(30));
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (settings.AllowedOrigins.Count > 0)
                policy.WithOrigins([.. settings.AllowedOrigins]).AllowAnyHeader().AllowAnyMethod();
        }));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await Results.Problem("Too many requests. Retry after the indicated delay.", statusCode: 429).ExecuteAsync(context.HttpContext);
            };
            options.AddPolicy(Commands, context => Budget(context, settings.CommandPermitLimit, settings.RateWindowSeconds));
            options.AddPolicy(Exports, context => Budget(context, settings.ExportPermitLimit, settings.RateWindowSeconds));
        });
    }

    /// <summary>Applies forwarding and TLS policy before routing, authentication and rate limiting.</summary>
    /// <param name="app">The application pipeline.</param>
    public static void UseApiTransport(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }
    }

    private static RateLimitPartition<(string? issuer, string? subject)> Budget(HttpContext context, int limit, int seconds) =>
        RateLimitPartition.GetFixedWindowLimiter((context.User.FindFirstValue("iss"), context.User.FindFirstValue("sub")), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(seconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
}
