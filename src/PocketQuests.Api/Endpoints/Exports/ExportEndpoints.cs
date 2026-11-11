using Microsoft.AspNetCore.Http.HttpResults;
using PocketQuests.Api.Hosting;
using PocketQuests.Contracts.Responses.Exports;
using PocketQuests.Services.Exports;
using System.Net.Mime;

namespace PocketQuests.Api.Endpoints.Exports;

/// <summary>Private on-demand downloads with explicit HTTP policy.</summary>
public static class ExportEndpoints
{
    /// <summary>Registers the existing consistent-snapshot export route.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapExportEndpoints(this WebApplication app) => app.MapGet("/api/export/{format}", Download)
        .WithName("ExportAccount").WithTags("PocketQuests.Api").RequireAuthorization().RequireRateLimiting(ApiHttpPolicy.Exports)
        .Produces<QuestExport>(200, MediaTypeNames.Application.Json, MediaTypeNames.Application.Zip)
        .Produces(400).Produces(404).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503)
        .AddOpenApiOperationTransformer((operation, context, token) =>
        {
            var content = operation.Responses!["200"].Content!;
            var zip = (Microsoft.OpenApi.OpenApiMediaType)content[MediaTypeNames.Application.Zip].CreateShallowCopy();
            zip.Schema = new Microsoft.OpenApi.OpenApiSchema
            {
                Type = Microsoft.OpenApi.JsonSchemaType.String,
                Format = "binary",
            };
            content[MediaTypeNames.Application.Zip] = zip;
            return Task.CompletedTask;
        });

    private static async Task<Results<FileContentHttpResult, BadRequest>> Download(string format, HttpContext context, IExportService service, CancellationToken token)
    {
        if (format is not "json" and not "csv")
            return TypedResults.BadRequest();
        var snapshot = await service.Read(token);
        context.Response.Headers.CacheControl = "no-store, private";
        return format == "json"
            ? TypedResults.File(ExportFormatting.JsonBytes(snapshot), MediaTypeNames.Application.Json, "pocket-quests.json")
            : TypedResults.File(ExportFormatting.CsvArchive(snapshot), MediaTypeNames.Application.Zip, "pocket-quests-csv.zip");
    }
}
