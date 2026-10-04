using HoneyDrunk.Identity.Abstractions.AccountLifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketQuests.Api.AccountLifecycle;
using PocketQuests.Api.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Lifecycle;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Services.Lifecycle;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.SchemaTests;

/// <summary>HTTP continuation, durable clock and private lifecycle boundaries.</summary>
public sealed partial class RelationalApiTests
{
    /// <summary>The enabled real hosted worker clears a retained backlog without any request-side write.</summary>
    /// <returns>Completion after background continuation and a successful read-only HTTP projection.</returns>
    [Fact]
    public async Task EnabledHostedMaintenanceMakesOverdueStateReadableWithoutClientMutation()
    {
        var owner = NewOwner();
        var identity = new AccountIdentity("honeydrunk-identity", owner);
        var store = fixture.Commands();
        await store.Initialize(identity, "Etc/UTC", Start);
        await store.Execute(identity, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1), Start);
        using var host = new Host(fixture.Connection, owner, reconciliationEnabled: true);
        host.Clock.Now = Start.AddDays(400);
        using var client = host.Client();
        Assert.Single(host.Services.GetServices<IHostedService>().OfType<ReconciliationMaintenance>());
        Assert.Single(host.Services.GetServices<IHostedService>().OfType<LifecycleMaintenance>());
        await using var db = fixture.Context();

        // Concurrent disposable DACPAC deployments compete with this hosted worker; this is a progress test, not a latency SLA.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        AccountEntity account;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
            account = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == owner, timeout.Token);
        }
        while (account.HasPendingReconciliation || account.ProjectionAsOfAt != host.Clock.Now);
        Assert.Equal(401, (await Read(client)).Occurrences.Length);
        Assert.Equal(1, await db.Set<CommandReceiptEntity>().CountAsync(r => r.AccountId == account.Id));
        Assert.Equal(account.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == account.Id)).RowVersion);
    }

    /// <summary>A late proof survives 503 responses and ordinary host restart until its exact pending command commits.</summary>
    /// <returns>Completion after recurrence, clock, receipt and proof checks.</returns>
    [Fact]
    public async Task PendingLateCommandAndAnchorReturnRetryable503WithoutConsumingProof()
    {
        var owner = NewOwner();
        QuestCommand complete;
        SyncAnchor anchor;
        using (var host = new Host(fixture.Connection, owner))
        using (var client = host.Client())
        {
            await Setup(client);
            var series = await Command(client, new(Guid.NewGuid(), QuestActions.SaveSeries, QuestId: "PQ-CAT-Q01", DueDate: "2026-01-01", ExpectedRevision: 0, SeriesId: Guid.NewGuid(), Cadence: Cadence.Days, Interval: 1));
            using var issued = await client.PostAsJsonAsync("/api/sync-anchor", new { DeviceId = Guid.NewGuid(), BootId = Guid.NewGuid(), DeviceUtc = Start });
            issued.EnsureSuccessStatusCode();
            anchor = (await issued.Content.ReadFromJsonAsync<SyncAnchor>(Json))!;
            complete = new(Guid.NewGuid(), QuestActions.Complete, series.Occurrences.Single().Occurrence.Id, RecordedTime: new(anchor.Id, anchor.BootId, 1, 1000.25, Start.AddMilliseconds(1000.25)));
            host.Clock.Now = Start.AddDays(400);
            using var read = await client.GetAsync(new Uri("/api/state", UriKind.Relative));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(1), read.Headers.RetryAfter!.Delta);
            using var export = await client.GetAsync(new Uri("/api/export/json", UriKind.Relative));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, export.StatusCode);
            using var pending = await client.PostAsJsonAsync("/api/commands", complete, Json);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, pending.StatusCode);
            using var pendingAnchor = await client.PostAsJsonAsync("/api/sync-anchor", new { DeviceId = Guid.NewGuid(), BootId = Guid.NewGuid(), DeviceUtc = host.Clock.Now });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, pendingAnchor.StatusCode);
            await using var db = fixture.Context();
            Assert.Equal(0, (await db.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).LastOrdinal);
            Assert.False(await db.Set<CommandReceiptEntity>().AnyAsync(r => r.Id == complete.OperationId));
        }

        using var restarted = new Host(fixture.Connection, owner);
        restarted.Clock.Now = Start.AddDays(400);
        using var retry = restarted.Client();
        using var stillPending = await retry.PostAsJsonAsync("/api/commands", complete, Json);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, stillPending.StatusCode);
        var committed = await Command(retry, complete);
        Assert.Equal(401, committed.Occurrences.Length);
        Assert.NotNull(committed.CompletionOutcome);
        await using var finalDb = fixture.Context();
        Assert.Equal(1, (await finalDb.Set<SyncAnchorEntity>().SingleAsync(a => a.Id == anchor.Id)).LastOrdinal);
        Assert.Equal(1, await finalDb.Set<CommandReceiptEntity>().CountAsync(r => r.Id == complete.OperationId));
        restarted.Clock.Now = Start.AddDays(399);
        var blocked = new QuestCommand(Guid.NewGuid(), QuestActions.ExpiryWarnings, ExpiryWarnings: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await retry.PostAsJsonAsync("/api/commands", blocked, Json)).StatusCode);
        Assert.False(await finalDb.Set<CommandReceiptEntity>().AnyAsync(r => r.Id == blocked.OperationId));
        using var badAnchor = await retry.PostAsJsonAsync("/api/sync-anchor", new { DeviceId = Guid.NewGuid(), BootId = Guid.NewGuid(), DeviceUtc = restarted.Clock.Now });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, badAnchor.StatusCode);
        restarted.Clock.Now = Start.AddDays(400);
        await Command(retry, blocked);
    }

    /// <summary>Selected lifecycle service fences HTTP, recovery requires resume and erasure prevents reinitialization.</summary>
    /// <returns>Completion after real private service transitions and public request checks.</returns>
    [Fact]
    public async Task LifecycleFencingRecoveryAndErasureApplyToAllPublicProductRoutes()
    {
        using var host = new Host(fixture.Connection);
        using var client = host.Client();
        await Setup(client);
        await Command(client, new(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: "PQ-CAT-Q01"));
        using var scope = host.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IQuestLifecycle>();
        host.Clock.Now = Start.AddMinutes(1);
        var pause = host.Clock.Now;
        await lifecycle.Receive(new(host.Owner, 1, IdentityProtocol.Inactive, pause, pause.AddHours(1), IdentityProtocol.ConsumerId, Capability(), pause), "private-test-ack", default);
        foreach (var route in new[] { "/api/state", "/api/export/json", "/api/export/csv", "/api/catalog" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri(route, UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/profile", new { Zone = "UTC" })).StatusCode);
        host.Users[host.Owner] = new(host.Owner, IdentityProtocol.Active, Start, 2, pause);
        host.Clock.Now = pause.AddMinutes(1);
        Assert.True((await Read(client)).Schedule.AccountPaused);
        Assert.False((await Command(client, new(Guid.NewGuid(), QuestActions.Resume))).Schedule.AccountPaused);
        host.Clock.Now = Start.AddDays(30);
        var erasing = new LifecycleIntent(host.Owner, 3, IdentityProtocol.Erasing, host.Clock.Now, host.Clock.Now.AddHours(1), IdentityProtocol.ConsumerId, Capability(), pause);
        await lifecycle.Receive(erasing, "private-test-ack", default);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/state", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/profile", new { Zone = "UTC" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/export/json", UriKind.Relative))).StatusCode);
        await using var db = fixture.Context();
        Assert.False(await db.Set<AccountEntity>().AnyAsync(a => a.IdentityUserId == host.Owner));
        Assert.Equal(host.Clock.Now, (await db.Set<ErasureMarkerEntity>().SingleAsync(m => m.Id == host.Owner)).CreatedAt);
    }

    private static string Capability() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
