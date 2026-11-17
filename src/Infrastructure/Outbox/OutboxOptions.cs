using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Outbox;

/// <summary>Bound from the <c>Outbox</c> section and validated at startup.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Whether this process delivers outbox messages. Events are always written to the outbox.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Wait between polls when there is nothing to process. A full batch is followed by the next one at once.</summary>
    [Range(50, 60_000)]
    public int PollingIntervalInMilliseconds { get; init; } = 1_000;

    [Range(1, 1_000)]
    public int BatchSize { get; init; } = 50;

    /// <summary>After this many failed attempts a message stops being retried (dead letter).</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Processed messages older than this are deleted.</summary>
    [Range(1, 365)]
    public int RetentionInDays { get; init; } = 7;
}
