using System.Diagnostics;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace Jobs.Filters;

/// <summary>
/// Gives every job execution its own trace (database calls become its children) and adds <c>JobId</c> and
/// <c>JobType</c> to the logs written while it runs. Failures are already logged by Hangfire.
/// </summary>
internal sealed class JobObservabilityFilter(ILoggerFactory loggerFactory) : IServerFilter
{
    private const string ActivityKey = "Jobs.Activity";
    private const string ScopeKey = "Jobs.LoggingScope";

    private readonly ILogger _logger = loggerFactory.CreateLogger<JobObservabilityFilter>();

    public void OnPerforming(PerformingContext context)
    {
        string jobId = context.BackgroundJob.Id;
        string jobType = JobsDiagnostics.DisplayName(context.BackgroundJob.Job.Type);

        // Synchronous on purpose: the Activity and the log scope (AsyncLocal) flow into the job that runs next.
        Activity? activity = JobsDiagnostics.ActivitySource.StartActivity($"job {jobType}", ActivityKind.Internal);
        activity?.SetTag("job.id", jobId);
        activity?.SetTag("job.type", jobType);

        context.Items[ActivityKey] = activity;
        context.Items[ScopeKey] = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = jobId,
            ["JobType"] = jobType
        });
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue(ScopeKey, out object? scope) && scope is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (context.Items.TryGetValue(ActivityKey, out object? value) && value is Activity activity)
        {
            if (context.Exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, context.Exception.Message);
            }

            activity.Dispose();
        }
    }
}
