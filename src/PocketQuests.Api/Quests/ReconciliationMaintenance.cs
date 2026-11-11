using Microsoft.EntityFrameworkCore;
using PocketQuests.Contracts.Models.Reconciliation;
using PocketQuests.Services.Reconciliation;

namespace PocketQuests.Api.Quests;

/// <summary>Advances bounded SQL recurrence and timed projections independently of ordinary GET requests.</summary>
/// <param name="scopes">Per-step service scopes.</param>
/// <param name="clock">Host clock.</param>
/// <param name="logger">Content-free worker diagnostics.</param>
public sealed class ReconciliationMaintenance(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ReconciliationMaintenance> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ReconciliationCursor? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(1);
            string? failureType = null;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var batch = await scope.ServiceProvider.GetRequiredService<IReconciliationService>().ReconcileAccounts(clock.GetUtcNow(), cursor, token: stoppingToken);
                cursor = batch.Next;
                if (cursor is not null || batch.Deliveries > 0)
                    delay = TimeSpan.FromSeconds(1);
            }
            catch (Exception error) when (error is System.Data.Common.DbException or InvalidOperationException or TimeoutException
                || (stoppingToken.IsCancellationRequested && error is DbUpdateException { InnerException: System.Data.Common.DbException }))
            {
                // Preserve only content-free failure metadata; provider exceptions may contain private SQL values.
                failureType = error.GetType().Name;
            }

            if (failureType is not null)
            {
                logger.LogError("Relational reconciliation failed ({FailureType}).", failureType);
            }

            // EF can wrap provider cancellation; the canceled stopping token still ends the worker through its normal cancellation path.
            await Task.Delay(delay, clock, stoppingToken);
        }
    }
}
