using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Jobs.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobs.Recurring;

/// <summary>
/// Synchronizes Hangfire with the code at startup: schedules every <see cref="IRecurringJob"/> and removes
/// recurring jobs that no longer exist in code (Hangfire would keep trying to run a deleted type). Idempotent.
/// </summary>
internal sealed class RecurringJobsSynchronizer(
    IServiceScopeFactory serviceScopeFactory,
    IRecurringJobManager recurringJobManager,
    JobStorage jobStorage,
    ILogger<RecurringJobsSynchronizer> logger) : IHostedService
{
    private static readonly RecurringJobOptions Options = new() { TimeZone = TimeZoneInfo.Utc };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        var registeredIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (IRecurringJob recurringJob in scope.ServiceProvider.GetServices<IRecurringJob>())
        {
            Type jobType = recurringJob.GetType();
            MethodInfo method = jobType.GetMethod(nameof(IRecurringJob.ExecuteAsync), [typeof(CancellationToken)])
                ?? throw new InvalidOperationException($"{jobType.Name} has no ExecuteAsync(CancellationToken).");

            // The CancellationToken argument is replaced by the job's token when it runs.
            var job = new Job(jobType, method, [CancellationToken.None], recurringJob.Queue);

            recurringJobManager.AddOrUpdate(recurringJob.Id, job, recurringJob.Cron, Options);
            registeredIds.Add(recurringJob.Id);

            logger.LogInformation(
                "Recurring job {RecurringJobId} scheduled with cron '{Cron}' (UTC) on queue {Queue}",
                recurringJob.Id,
                recurringJob.Cron,
                recurringJob.Queue);
        }

        RemoveObsoleteJobs(registeredIds);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void RemoveObsoleteJobs(HashSet<string> registeredIds)
    {
        using IStorageConnection connection = jobStorage.GetConnection();

        foreach (RecurringJobDto existing in connection.GetRecurringJobs().Where(job => !registeredIds.Contains(job.Id)))
        {
            recurringJobManager.RemoveIfExists(existing.Id);

            logger.LogWarning("Recurring job {RecurringJobId} no longer exists in code and was removed", existing.Id);
        }
    }
}
