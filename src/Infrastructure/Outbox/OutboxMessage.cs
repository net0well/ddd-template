namespace Infrastructure.Outbox;

/// <summary>
/// A domain event saved in the same transaction as the change that raised it, delivered later by the
/// <see cref="OutboxProcessor"/> (at least once).
/// </summary>
internal sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Full name of the event type, resolved by <see cref="DomainEventSerializer"/>.</summary>
    public string Type { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime OccurredOnUtc { get; init; }

    /// <summary>W3C <c>traceparent</c> of the request that raised the event, so processing joins its trace.</summary>
    public string? TraceParent { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    /// <summary>Set when the message stopped being retried. Clear it (and the attempts) to replay the message.</summary>
    public DateTime? DeadLetteredOnUtc { get; set; }

    public string? Error { get; set; }
}

/// <summary>Records that a handler already processed a message, so a retry does not run it again.</summary>
internal sealed class OutboxMessageConsumer
{
    public Guid OutboxMessageId { get; init; }

    public string Handler { get; init; } = string.Empty;
}
