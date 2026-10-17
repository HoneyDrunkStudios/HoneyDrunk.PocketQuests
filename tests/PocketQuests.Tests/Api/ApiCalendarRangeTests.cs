using Microsoft.Extensions.Logging;
using System.Net;
using Xunit.Abstractions;

namespace PocketQuests.Tests.Api;

/// <summary>Caller-selected calendar boundaries must produce validation errors, not retryable server failures.</summary>
/// <param name="output">The recorded before/after HTTP evidence.</param>
public sealed class ApiCalendarRangeTests(ITestOutputHelper output)
{
    /// <summary>Rejects the three independently reproduced invalid ranges and retains ordinary dates.</summary>
    /// <param name="date">The selected calendar date.</param>
    /// <param name="time">The selected clock time.</param>
    /// <param name="zone">The selected IANA zone.</param>
    /// <param name="expected">The expected HTTP status.</param>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Theory]
    [InlineData("0000-01-01", "12:00", "UTC", 400)]
    [InlineData("9999-12-31", "23:59", "America/New_York", 400)]
    [InlineData("0001-01-01", "00:00", "Asia/Tokyo", 400)]
    [InlineData("2026-10-04", "12:00", "UTC", 200)]
    [InlineData("0001-01-01", "00:00", "UTC", 200)]
    [InlineData("9999-12-31", "23:59", "UTC", 200)]
    public async Task PlanningValidatesTheSupportedInstantRange(string date, string time, string zone, int expected)
    {
        await using var host = await ApiTestHost.Start();
        var route = new Uri($"/api/planning/clock?date={date}&time={time}&zone={Uri.EscapeDataString(zone)}", UriKind.Relative);
        using var response = await host.Client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{route} => {(int)response.StatusCode}: {body}");
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.level == LogLevel.Error);
        if (expected == (int)HttpStatusCode.BadRequest)
            Assert.Contains("supported", body, StringComparison.OrdinalIgnoreCase);
    }
}
