using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;

namespace PocketQuests.Api.Errors;

/// <summary>Maps explicit rejections and reports unexpected failures without recording private input.</summary>
/// <param name="next">The remaining request pipeline.</param>
/// <param name="logger">The safe diagnostic sink.</param>
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    /// <summary>Executes the request with a stable error boundary.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>The completed request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Abort();
        }
        catch (Exception error)
        {
            if (context.Response.HasStarted)
            {
                LogUnexpected(error);
                context.Abort();
                return;
            }

            context.Response.Clear();
            IResult result;
            switch (error)
            {
                case UnauthorizedAccessException:
                    result = Results.Unauthorized();
                    break;
                case ReconciliationPendingException:
                    context.Response.Headers.RetryAfter = "1";
                    result = Results.Problem("Recurring deliveries are being reconciled. Retry the exact pending request.", statusCode: 503);
                    break;
                case SyncClockNotReadyException clock:
                    result = Results.Problem(clock.Message, statusCode: 503);
                    break;
                case QuestValidationException validation:
                    result = Results.Problem(validation.Message, statusCode: 400);
                    break;
                case QuestConflictException:
                    result = Results.Problem("The action conflicts with current quest state. Refresh and check the deadline or Undo window.", statusCode: 409);
                    break;
                case QuestNotFoundException:
                    result = Results.NotFound();
                    break;
                case BadHttpRequestException badRequest:
                    result = Results.Problem("The request could not be read. Check the request format.", statusCode: badRequest.StatusCode);
                    break;
                default:
                    var diagnosticId = LogUnexpected(error);
                    result = Results.Problem("An unexpected server error occurred. Retry the request later.", statusCode: 500, extensions: new Dictionary<string, object?> { ["diagnosticId"] = diagnosticId });
                    break;
            }

            await result.ExecuteAsync(context);
        }
    }

    private string LogUnexpected(Exception error)
    {
        // Exception messages, stacks, URLs, claims and headers can contain credentials or user text.
        // Keep type and a server-generated diagnostic ID; never pass the raw exception to a sink.
        var diagnosticId = Guid.NewGuid().ToString("N");
        logger.LogError("Unexpected API failure {ExceptionType}; diagnostic {DiagnosticId}", error.GetType().FullName, diagnosticId);
        return diagnosticId;
    }
}
