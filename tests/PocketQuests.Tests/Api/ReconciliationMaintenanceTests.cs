using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PocketQuests.Api.Quests;
using PocketQuests.Contracts.Models.Reconciliation;
using PocketQuests.Services.Reconciliation;
using System.Data.Common;

namespace PocketQuests.Tests.Api;

/// <summary>Host shutdown preserves provider failure diagnostics without turning cancellation into a worker fault.</summary>
public sealed class ReconciliationMaintenanceTests
{
    /// <summary>EF can wrap an in-flight SQL cancellation instead of returning OperationCanceledException.</summary>
    /// <returns>Completion after the host's stopping token cancels the service and its scope is disposed.</returns>
    [Fact]
    public async Task ShutdownReportsWrappedProviderFailureAndDisposesScope()
    {
        using var service = new ReconciliationStub(new DbUpdateException("private EF detail", new ProviderFailure()));
        await using var services = Services(service);
        using var logs = new ApiTestLog();
        using var logging = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var worker = new ReconciliationMaintenance(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, logging.CreateLogger<ReconciliationMaintenance>());
        await worker.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ExecuteTask!);

        Assert.True(service.Token.IsCancellationRequested);
        Assert.True(service.Disposed);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.level);
        Assert.Equal("Relational reconciliation failed (DbUpdateException).", entry.message);
        Assert.Null(entry.error);
    }

    /// <summary>A database update failure before shutdown remains fatal instead of being swallowed as cancellation.</summary>
    /// <returns>Completion after the original failure escapes the worker.</returns>
    [Fact]
    public async Task ProviderFailureWithoutHostCancellationStillFaultsWorker()
    {
        var failure = new DbUpdateException("private EF detail", new ProviderFailure());
        using var service = new ReconciliationStub(failure, waitForShutdown: false);
        await using var services = Services(service);
        using var logging = LoggerFactory.Create(_ => { });
        using var worker = new ReconciliationMaintenance(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, logging.CreateLogger<ReconciliationMaintenance>());
        await worker.StartAsync(CancellationToken.None);

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() => worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Same(failure, actual);
        Assert.False(service.Token.IsCancellationRequested);
        Assert.True(service.Disposed);
    }

    /// <summary>Cancellation does not hide an EF failure that has no provider cancellation shape.</summary>
    /// <returns>Completion after the unrelated failure escapes during shutdown.</returns>
    [Fact]
    public async Task ShutdownDoesNotHideUnrelatedUpdateFailure()
    {
        var failure = new DbUpdateException("Unrelated update failure.");
        using var service = new ReconciliationStub(failure);
        await using var services = Services(service);
        using var logging = LoggerFactory.Create(_ => { });
        using var worker = new ReconciliationMaintenance(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, logging.CreateLogger<ReconciliationMaintenance>());
        await worker.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        var actual = await Assert.ThrowsAsync<DbUpdateException>(() => worker.ExecuteTask!);

        Assert.Same(failure, actual);
        Assert.True(service.Token.IsCancellationRequested);
        Assert.True(service.Disposed);
    }

    /// <summary>Ordinary cancellation still follows BackgroundService's canceled-task shutdown contract.</summary>
    /// <returns>Completion after cancellation is observed without a provider-failure diagnostic.</returns>
    [Fact]
    public async Task ShutdownPreservesOrdinaryCancellation()
    {
        using var service = new ReconciliationStub(new OperationCanceledException("Expected host stop."));
        await using var services = Services(service);
        using var logs = new ApiTestLog();
        using var logging = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var worker = new ReconciliationMaintenance(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, logging.CreateLogger<ReconciliationMaintenance>());
        await worker.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ExecuteTask!);

        Assert.True(service.Token.IsCancellationRequested);
        Assert.True(service.Disposed);
        Assert.Empty(logs.Entries);
    }

    /// <summary>Recoverable provider failures retain the existing content-free diagnostic before shutdown.</summary>
    /// <returns>Completion after the failure is reported and the worker stops through normal cancellation.</returns>
    [Fact]
    public async Task DirectProviderFailureKeepsContentFreeDiagnostics()
    {
        using var service = new ReconciliationStub(new ProviderFailure(), waitForShutdown: false);
        await using var services = Services(service);
        using var logs = new ApiTestLog();
        using var logging = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var worker = new ReconciliationMaintenance(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, logging.CreateLogger<ReconciliationMaintenance>());
        await worker.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ExecuteTask!);

        Assert.True(service.Disposed);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.level);
        Assert.Equal("Relational reconciliation failed (ProviderFailure).", entry.message);
        Assert.Null(entry.error);
    }

    private static ServiceProvider Services(ReconciliationStub service) => new ServiceCollection()
        .AddScoped<IReconciliationService>(_ => service)
        .BuildServiceProvider();

    private sealed class ProviderFailure : DbException;

    private sealed class ReconciliationStub(Exception failure, bool waitForShutdown = true) : IReconciliationService, IDisposable
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationToken Token { get; private set; }

        internal bool Disposed { get; private set; }

        public async Task<ReconciliationBatch> ReconcileAccounts(DateTimeOffset now, ReconciliationCursor? after = null, int maximumAccounts = 10, int maximumDeliveries = 100, CancellationToken token = default)
        {
            Token = token;
            Started.SetResult();
            if (waitForShutdown)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Reproduce the EF/provider exception shape observed in the hosted SQL artifact.
                }
            }

            throw failure;
        }

        public void Dispose() => Disposed = true;
    }
}
