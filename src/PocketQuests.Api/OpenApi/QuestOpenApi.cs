using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using PocketQuests.Api.Administration;

namespace PocketQuests.Api.OpenApi;

/// <summary>Generates the public document from registered HTTP endpoints and their explicit contracts.</summary>
public static class QuestOpenApi
{
    /// <summary>Registers first-party endpoint-driven OpenAPI generation.</summary>
    /// <param name="services">The API service registrations.</param>
    public static void AddQuestOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, context, token) =>
            {
                document.Info = new() { Title = "Pocket Quests API", Version = "1.0" };
                document.Servers = [];
                document.Components ??= new();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["IdentityBearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    Description = "Access token resolved by HoneyDrunk Identity.",
                };
                document.Components.SecuritySchemes["AdminSession"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Cookie,
                    Name = "PocketQuests.Admin",
                    Description = "Configured operator's HttpOnly session, obtained through /admin/login.",
                };
                document.Components.SecuritySchemes["AdminCsrf"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-CSRF-TOKEN",
                    Description = "Anti-forgery proof from /api/admin/session; required with the administrative cookie.",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, token) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
                {
                    var requirement = new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("IdentityBearer", context.Document)] = [],
                    };
                    operation.Security = [requirement];
                    if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any(data => data.Policy == AdminWeb.Policy))
                    {
                        operation.Security.Add(new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("AdminSession", context.Document)] = [],
                            [new OpenApiSecuritySchemeReference("AdminCsrf", context.Document)] = [],
                        });
                        operation.Description = "Only the explicitly configured canonical Identity operator account is permitted.";
                    }

                    operation.Responses ??= new();
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication required" });
                    operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
                }

                return Task.CompletedTask;
            });
        });
    }
}
