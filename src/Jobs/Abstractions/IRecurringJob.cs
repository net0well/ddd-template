namespace Jobs.Abstractions;

/// <summary>
/// A job that runs on a schedule. Implementations in this assembly are registered and scheduled
/// automatically at startup — prefer inheriting <see cref="JobBase"/>.
/// </summary>
public interface IRecurringJob
{
    /// <summary>Stable id in Hangfire. Changing it creates a new recurring job.</summary>
    string Id { get; }

    /// <summary>Cron expression, evaluated in UTC (see <c>Hangfire.Cron</c> helpers).</summary>
    string Cron { get; }

    string Queue => Queues.Default;

    Task ExecuteAsync(CancellationToken cancellationToken);
}
