using HoneyDrunk.Payments.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PocketQuests.Api.Hosting;
using PocketQuests.Contracts.Requests.Billing;
using PocketQuests.Contracts.Responses.Billing;
using PocketQuests.Services.Billing;

namespace PocketQuests.Api.Endpoints.Billing;

internal static class BillingEndpoints
{
    private const int WebhookCharacterLimit = 128 * 1024;

    internal static void MapBilling(this WebApplication app)
    {
        var account = app.MapGroup("/api/billing").WithTags("PocketQuests.Api")
            .RequireAuthorization().RequireRateLimiting(ApiHttpPolicy.Commands);
        app.MapGet("/api/billing", Read).WithTags("PocketQuests.Api").RequireAuthorization()
            .RequireRateLimiting(ApiHttpPolicy.Commands).WithName("ReadBillingStatus").ProducesProblem(503);
        account.MapPost("/checkout", Checkout).WithName("StartBillingCheckout").ProducesProblem(400).ProducesProblem(503);
        app.MapPost("/api/billing/stripe/webhook", Webhook).AllowAnonymous().ExcludeFromDescription();
    }

    private static async Task<Ok<BillingStatus>> Read(HttpContext context, [FromServices] BillingService billing, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await billing.Read(token));
    }

    private static async Task<Ok<BillingCheckout>> Checkout(HttpContext context, StartBillingCheckout request, [FromServices] BillingService billing, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await billing.StartCheckout(request.Interval, token));
    }

    private static async Task<IResult> Webhook(
        HttpRequest request,
        IOptions<BillingOptions> options,
        [FromServices] IPaymentWebhookEventValidator validator,
        [FromServices] BillingService billing,
        CancellationToken token)
    {
        if (!options.Value.SandboxEnabled)
            return Results.NotFound();
        if (!request.Headers.TryGetValue("Stripe-Signature", out var signature) || signature.Count != 1
            || request.ContentLength > WebhookCharacterLimit)
            return Results.BadRequest();

        using var reader = new StreamReader(request.Body);
        var buffer = new char[WebhookCharacterLimit + 1];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), token);
        if (count == 0 || count > WebhookCharacterLimit)
            return Results.BadRequest();

        PaymentWebhookEventSnapshot message;
        try
        {
            message = await validator.ValidateWebhookEventAsync(new string(buffer, 0, count), signature.ToString(), token);
        }
        catch (Exception error) when (error is not BillingUnavailableException && !token.IsCancellationRequested)
        {
            return Results.BadRequest();
        }

        if (message.Livemode)
            return Results.BadRequest();
        await billing.ApplyEvent(message, token);
        return Results.NoContent();
    }
}
