using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Time;

namespace Infrastructure.Outbox;

/// <summary>
/// Degraded, never unhealthy (the API still serves requests), when messages became dead letters or the oldest
/// pending message waits longer than expected, which usually means no processor is running.
/// </summary>
internal sealed class OutboxHealthCheck(ApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider) : IHealthCheck
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        int deadLetters = await dbContext.OutboxMessages.CountAsync(message => message.DeadLetteredOnUtc != null, cancellationToken);

        DateTime? oldestPending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedOnUtc == null && message.DeadLetteredOnUtc == null)
            .MinAsync(message => (DateTime?)message.OccurredOnUtc, cancellationToken);

        TimeSpan delay = oldestPending is null ? TimeSpan.Zero : dateTimeProvider.UtcNow - oldestPending.Value;

        var data = new Dictionary<string, object>
        {
            ["deadLetters"] = deadLetters,
            ["oldestPendingSeconds"] = (int)delay.TotalSeconds
        };

        if (deadLetters > 0)
        {
            return HealthCheckResult.Degraded($"{deadLetters} outbox message(s) are dead letters.", data: data);
        }

        return delay > MaxDelay
            ? HealthCheckResult.Degraded($"Outbox delivery is {(int)delay.TotalMinutes} minute(s) behind.", data: data)
            : HealthCheckResult.Healthy(data: data);
    }
}
