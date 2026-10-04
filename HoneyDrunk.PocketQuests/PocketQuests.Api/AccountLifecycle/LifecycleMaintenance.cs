using PocketQuests.Data.AccountLifecycle;

namespace PocketQuests.Api.AccountLifecycle;

/// <summary>Removes short-lived acknowledgment envelopes and expired minimal markers.</summary>
public sealed class LifecycleMaintenance(IServiceScopeFactory scopes, ILogger<LifecycleMaintenance> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IQuestLifecycle>().Prune(stoppingToken);
            }
            catch (Exception error) when (error is System.Data.Common.DbException or InvalidOperationException or TimeoutException)
            {
                logger.LogError("Lifecycle retention maintenance failed ({FailureType}).", error.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
