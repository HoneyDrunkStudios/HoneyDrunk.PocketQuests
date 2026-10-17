using Microsoft.Extensions.Logging;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Synchronization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace PocketQuests.Tests.Api;

/// <summary>HTTP error classification and private-data regression tests.</summary>
public sealed class ApiErrorTests
{
    /// <summary>Ordinary library exceptions must produce sanitized, logged server errors.</summary>
    /// <param name="kind">The unexpected exception type.</param>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Theory]
    [InlineData("argument")]
    [InlineData("operation")]
    [InlineData("missing")]
    [InlineData("unknown")]
    public async Task UnexpectedFailuresAreLoggedAs500WithoutPrivateDetails(string kind)
    {
        await using var host = await ApiTestHost.Start();
        const string secret = "synthetic-private-token-and-user-title";
        host.Store.Failure = kind switch
        {
            "argument" => new ArgumentException(secret),
            "operation" => new InvalidOperationException(secret),
            "missing" => new KeyNotFoundException(secret),
            _ => new IOException(secret),
        };
        var response = await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body);
        var id = JsonNode.Parse(body)!["diagnosticId"]!.GetValue<string>();
        Assert.True(Guid.TryParseExact(id, "N", out _));
        var error = Assert.Single(host.Logs.Entries, item => item.level == LogLevel.Error);
        Assert.Contains(id, error.message);
        Assert.Contains(host.Store.Failure.GetType().FullName!, error.message);
        Assert.Null(error.error);
        Assert.All(host.Logs.Entries, item => Assert.DoesNotContain(secret, item.message));
    }

    /// <summary>Only explicit domain rejections retain their client status.</summary>
    /// <param name="kind">The rejection kind.</param>
    /// <param name="status">The compatible status code.</param>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Theory]
    [InlineData("validation", 400)]
    [InlineData("conflict", 409)]
    [InlineData("missing", 404)]
    [InlineData("clock", 503)]
    [InlineData("reconciliation", 503)]
    public async Task ExplicitRejectionsKeepTheirStatus(string kind, int status)
    {
        await using var host = await ApiTestHost.Start();
        host.Store.Failure = kind switch
        {
            "validation" => new QuestValidationException("Choose a valid date."),
            "conflict" => new QuestConflictException("Expired deadline."),
            "missing" => new QuestNotFoundException(),
            "clock" => new SyncClockNotReadyException("Server time is behind committed account history."),
            _ => new ReconciliationPendingException("Pending."),
        };
        var response = await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(status, (int)response.StatusCode);
        if (kind == "clock")
            Assert.Contains(host.Store.Failure.Message, await response.Content.ReadAsStringAsync());
        if (kind == "reconciliation")
            Assert.Equal(TimeSpan.FromSeconds(1), response.Headers.RetryAfter?.Delta);
        Assert.DoesNotContain(host.Logs.Entries, item => item.level == LogLevel.Error);
    }

    /// <summary>Malformed JSON and typed query binding are client errors with no parser details.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task MalformedAndDomainInvalidInputReturn400()
    {
        await using var host = await ApiTestHost.Start();
        using var content = new StringContent("{\"operationId\":\"synthetic-private-token\"}", Encoding.UTF8, "application/json");
        var malformed = await host.Client.PostAsync(new Uri("/api/commands", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain("synthetic-private-token", await malformed.Content.ReadAsStringAsync());
        Assert.Equal(0, host.Store.Commands);
        var invalid = await host.Client.PostAsJsonAsync("/api/commands", new { operationId = Guid.NewGuid(), action = "invalid-domain-action" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(new Uri("/api/planning/clock?date=bad&time=bad&zone=UTC", UriKind.Relative))).StatusCode);
        Assert.All(host.Logs.Entries, item => Assert.DoesNotContain("synthetic-private-token", item.message));
    }

    /// <summary>Missing and null required values cannot reach command mapping or storage.</summary>
    /// <param name="json">The malformed request.</param>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"operationId\":\"00000000-0000-0000-0000-000000000000\",\"action\":null}")]
    [InlineData("{\"operationId\":\"00000000-0000-0000-0000-000000000000\",\"action\":\"test\",\"definition\":{}}")]
    [InlineData("{\"operationId\":\"00000000-0000-0000-0000-000000000000\",\"action\":\"test\",\"definition\":{\"id\":\"test\",\"title\":\"test\",\"criterion\":\"test\",\"categoryId\":\"c01\",\"rank\":\"F\",\"effort\":\"Small\",\"attributes\":[null],\"skills\":[]}}")]
    public async Task MissingRequiredValuesReturn400(string json)
    {
        await using var host = await ApiTestHost.Start();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsync(new Uri("/api/commands", UriKind.Relative), content)).StatusCode);
        Assert.Equal(0, host.Store.Commands);
    }

    /// <summary>Anonymous requests are challenged; authentication failures also reach the error boundary.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task AuthenticationRunsInsideTheErrorBoundary()
    {
        await using var host = await ApiTestHost.Start();
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-auth-failure");
        var response = await host.Client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("synthetic-private-token", await response.Content.ReadAsStringAsync());
        Assert.Single(host.Logs.Entries, item => item.level == LogLevel.Error);
    }
}
