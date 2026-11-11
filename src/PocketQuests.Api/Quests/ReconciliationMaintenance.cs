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
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var batch = await scope.ServiceProvider.GetRequiredService<IReconciliationService>().ReconcileAccounts(clock.GetUtcNow(), cursor, token: stoppingToken);
                cursor = batch.Next;
                if (cursor is not null || batch.Deliveries > 0)
                    delay = TimeSpan.FromSeconds(1);
            }
            catch (DbUpdateException error) when (stoppingToken.IsCancellationRequested && error.InnerException is System.Data.Common.DbException)
            {
                // EF can wrap a provider cancellation during host shutdown; retain diagnostics without faulting the stopped worker.
                logger.LogError("Relational reconciliation failed ({FailureType}).", error.GetType().Name);
                return;
            }
            catch (Exception error) when (error is System.Data.Common.DbException or InvalidOperationException or TimeoutException)
            {
                logger.LogError("Relational reconciliation failed ({FailureType}).", error.GetType().Name);
            }

            await Task.Delay(delay, clock, stoppingToken);
        }
    }
}
