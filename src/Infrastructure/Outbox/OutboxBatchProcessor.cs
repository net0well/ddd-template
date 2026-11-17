using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.DependencyInjection;
using SharedKernel.Events;
using SharedKernel.Time;

namespace Infrastructure.Outbox;

/// <summary>
/// Claims one batch of pending messages (<c>FOR UPDATE SKIP LOCKED</c>, so instances never take the same rows),
/// delivers each one to its handlers and records the outcome in a single save.
/// </summary>
/// <remarks>
/// The rows stay locked while the handlers run and are released by the commit, or by the database if the
/// process dies. Handlers must therefore be quick: slow work belongs in a background job.
/// </remarks>
internal sealed class OutboxBatchProcessor(
    ApplicationDbContext context,
    DomainEventSerializer serializer,
    IServiceScopeFactory serviceScopeFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBatchProcessor> logger) : IInjectScoped
{
    private const int CleanupBatchSize = 5_000;

    private static readonly ActivitySource ActivitySource = new(OutboxDiagnostics.ActivitySourceName);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly OutboxOptions _options = options.Value;

    /// <returns>How many messages were claimed.</returns>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(ProcessBatchAsync, cancellationToken);
    }

    /// <summary>Deletes processed messages older than the retention, in chunks to keep each statement short.</summary>
    /// <returns>How many messages were deleted.</returns>
    public async Task<int> DeleteProcessedAsync(CancellationToken cancellationToken)
    {
        DateTime cutoff = dateTimeProvider.UtcNow.AddDays(-_options.RetentionInDays);
        int total = 0;
        int deleted;

        do
        {
            deleted = await context.OutboxMessages
                .Where(message => message.ProcessedOnUtc < cutoff)
                .OrderBy(message => message.ProcessedOnUtc)
                .Take(CleanupBatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            total += deleted;
        }
        while (deleted == CleanupBatchSize);

        return total;
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        // READ COMMITTED (the default): stricter isolation makes SKIP LOCKED fail with serialization errors.
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        DateTime utcNow = dateTimeProvider.UtcNow;

        // Same predicate as the partial index ix_outbox_messages_pending, ordered by its key.
        List<OutboxMessage> messages = await context.OutboxMessages
            .FromSql($"""
                SELECT * FROM public.outbox_messages
                WHERE processed_on_utc IS NULL
                  AND dead_lettered_on_utc IS NULL
                  AND (next_attempt_on_utc IS NULL OR next_attempt_on_utc <= {utcNow})
                ORDER BY occurred_on_utc
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .TagWith(OutboxDiagnostics.PollingQueryTag)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        Guid[] ids = [.. messages.Select(message => message.Id)];

        HashSet<(Guid, string)> consumed = [.. (await context.OutboxMessageConsumers
            .AsNoTracking()
            .Where(consumer => ids.Contains(consumer.OutboxMessageId))
            .ToListAsync(cancellationToken))
            .Select(consumer => (consumer.OutboxMessageId, consumer.Handler))];

        foreach (OutboxMessage message in messages)
        {
            await DeliverAsync(message, consumed, utcNow, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    private async Task DeliverAsync(
        OutboxMessage message,
        HashSet<(Guid, string)> consumed,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        IDomainEvent? domainEvent;

        try
        {
            domainEvent = serializer.Deserialize(message.Type, message.Content);
        }
        catch (JsonException exception)
        {
            DeadLetter(message, utcNow, $"Invalid content: {exception.Message}");
            return;
        }

        if (domainEvent is null)
        {
            DeadLetter(message, utcNow, $"Unknown event type '{message.Type}'.");
            return;
        }

        // Continues the trace of the request that raised the event: handler logs share its trace_id.
        using Activity? activity = ActivitySource.StartActivity(
            $"outbox {domainEvent.GetType().Name}",
            ActivityKind.Consumer,
            message.TraceParent);
        activity?.SetTag("messaging.system", "outbox");
        activity?.SetTag("messaging.message.id", message.Id);
        activity?.SetTag("outbox.attempt", message.Attempts + 1);

        string? failure = null;

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        foreach (object handler in DomainEventHandlerInvoker.GetHandlers(scope.ServiceProvider, domainEvent.GetType()))
        {
            string handlerName = handler.GetType().FullName!;

            if (consumed.Contains((message.Id, handlerName)))
            {
                continue;
            }

            try
            {
                await DomainEventHandlerInvoker.InvokeAsync(handler, domainEvent, cancellationToken);

                context.OutboxMessageConsumers.Add(new OutboxMessageConsumer { OutboxMessageId = message.Id, Handler = handlerName });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The other handlers still run; only the failed ones run again on the retry.
                failure = $"{handler.GetType().Name}: {exception.Message}";

                activity?.AddException(exception);
                logger.LogError(exception, "Outbox handler {Handler} failed for message {OutboxMessageId}", handlerName, message.Id);
            }
        }

        if (failure is null)
        {
            message.ProcessedOnUtc = utcNow;
            message.Error = null;
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error, failure);

        message.Attempts++;
        message.Error = failure;

        if (message.Attempts >= _options.MaxAttempts)
        {
            DeadLetter(message, utcNow, failure);
            return;
        }

        message.NextAttemptOnUtc = utcNow + Backoff(message.Attempts);
    }

    private void DeadLetter(OutboxMessage message, DateTime utcNow, string error)
    {
        message.DeadLetteredOnUtc = utcNow;
        message.NextAttemptOnUtc = null;
        message.Error = error;

        logger.LogError(
            "Outbox message {OutboxMessageId} ({Type}) is a dead letter after {Attempts} attempt(s): {Error}",
            message.Id,
            message.Type,
            message.Attempts,
            error);
    }

    // 2 s, 4 s, 8 s... capped at five minutes.
    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, attempts)));
}
