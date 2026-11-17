using System.ComponentModel.DataAnnotations;

namespace Jobs;

/// <summary>Bound from the <c>Jobs</c> section and validated at startup.</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Database schema where Hangfire keeps its tables (created by Hangfire itself, not by EF migrations).</summary>
    [Required]
    [RegularExpression("^[a-z_][a-z0-9_]*$")]
    public string Schema { get; init; } = "hangfire";

    public JobsServerOptions Server { get; init; } = new();

    public JobsDashboardOptions Dashboard { get; init; } = new();
}

public sealed class JobsServerOptions
{
    /// <summary>
    /// Whether this process executes jobs. When off it still enqueues them — so the API and the workers can
    /// be scaled separately.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public int WorkerCount { get; init; } = 5;

    public int QueuePollIntervalInSeconds { get; init; } = 15;
}

public sealed class JobsDashboardOptions
{
    public bool Enabled { get; init; } = true;

    public string Path { get; init; } = "/hangfire";

    /// <summary>Basic-auth credentials. Required when the dashboard is enabled.</summary>
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
