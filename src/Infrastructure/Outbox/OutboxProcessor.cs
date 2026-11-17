using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Outbox;

/// <summary>
/// Polls the outbox: drains full batches back to back, waits the polling interval when it is empty, and deletes
/// old processed messages once an hour. While the database is unavailable it backs off up to a minute.
/// </summary>
internal sealed class OutboxProcessor(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxFailureDelay = TimeSpan.FromMinutes(1);

    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromMilliseconds(_options.PollingIntervalInMilliseconds);
        DateTime nextCleanup = timeProvider.GetUtcNow().UtcDateTime;
        int consecutiveFailures = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                consecutiveFailures = await DrainAsync(stoppingToken) ? 0 : consecutiveFailures + 1;

                if (consecutiveFailures == 0 && timeProvider.GetUtcNow().UtcDateTime >= nextCleanup)
                {
                    await CleanupAsync(stoppingToken);
                    nextCleanup = timeProvider.GetUtcNow().UtcDateTime + CleanupInterval;
                }

                await Task.Delay(consecutiveFailures == 0 ? pollingInterval : FailureDelay(consecutiveFailures), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutting down; unfinished batches roll back and are delivered after the restart.
        }
    }

    /// <returns><see langword="false"/> when polling failed.</returns>
    private async Task<bool> DrainAsync(CancellationToken stoppingToken)
    {
        try
        {
            int claimed;

            do
            {
                await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

                claimed = await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().ProcessAsync(stoppingToken);
            }
            while (claimed == _options.BatchSize && !stoppingToken.IsCancellationRequested);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The loop must survive a database outage; the messages stay pending for the next poll.
            logger.LogError(exception, "Outbox polling failed");
            return false;
        }
    }

    private async Task CleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

            int deleted = await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().DeleteProcessedAsync(stoppingToken);

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} processed outbox message(s)", deleted);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Outbox cleanup failed");
        }
    }

    // 2 s, 4 s, 8 s... up to a minute, so an outage does not flood the logs.
    private static TimeSpan FailureDelay(int consecutiveFailures) =>
        TimeSpan.FromSeconds(Math.Min(MaxFailureDelay.TotalSeconds, Math.Pow(2, consecutiveFailures)));
}
