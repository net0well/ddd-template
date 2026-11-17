namespace Jobs;

/// <summary>Hangfire queues. Workers serve them in this order, so earlier queues have priority.</summary>
public static class Queues
{
    public const string Default = "default";

    /// <summary>Housekeeping that can wait (cleanups, purges).</summary>
    public const string Maintenance = "maintenance";

    internal static readonly string[] All = [Default, Maintenance];
}
