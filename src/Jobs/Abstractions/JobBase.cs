using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using SharedKernel.Results;

namespace Jobs.Abstractions;

/// <summary>
/// Base for recurring jobs: logs start, end and duration, never lets two executions overlap, and turns a
/// failed <see cref="CommandResult"/> into a job failure so Hangfire retries it.
/// </summary>
/// <remarks>
/// Retries can be tuned per job with <c>[AutomaticRetry]</c> on the derived class.
/// </remarks>
public abstract class JobBase(ILogger logger) : IRecurringJob
{
    private const int ConcurrencyLockTimeoutInSeconds = 600;

    public abstract string Id { get; }

    public abstract string Cron { get; }

    public virtual string Queue => Queues.Default;

    [DisableConcurrentExecution(ConcurrencyLockTimeoutInSeconds)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Job {RecurringJobId} started", Id);

        long startedAt = Stopwatch.GetTimestamp();

        await RunAsync(cancellationToken);

        logger.LogInformation(
            "Job {RecurringJobId} finished in {ElapsedMilliseconds:0.0} ms",
            Id,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    protected abstract Task RunAsync(CancellationToken cancellationToken);

    /// <summary>Throws when the use case failed, so Hangfire records the failure and retries.</summary>
    protected void EnsureSuccess(CommandResult result) => result.ThrowIfFailed(Id);
}
