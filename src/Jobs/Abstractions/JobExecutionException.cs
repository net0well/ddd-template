using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Jobs.Abstractions;

/// <summary>A job's use case returned notifications. Thrown on purpose: it is how Hangfire marks a retry.</summary>
public sealed class JobExecutionException : Exception
{
    public JobExecutionException(string jobName, IReadOnlyCollection<Notification> notifications)
        : base($"{jobName} failed: {string.Join("; ", notifications)}")
    {
        Notifications = notifications;
    }

    public IReadOnlyCollection<Notification> Notifications { get; }
}

internal static class CommandResultExtensions
{
    public static void ThrowIfFailed(this CommandResult result, string jobName)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Success)
        {
            throw new JobExecutionException(jobName, result.Notifications);
        }
    }
}
