namespace Infrastructure.Outbox;

public static class OutboxDiagnostics
{
    /// <summary>Name to register in OpenTelemetry tracing (<c>AddSource</c>) to trace outbox deliveries.</summary>
    public const string ActivitySourceName = "Outbox";

    /// <summary>SQL comment on the polling query, so log filters can drop it (it runs every second).</summary>
    public const string PollingQueryTag = "outbox:poll";
}
