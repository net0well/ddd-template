using Application.Abstractions.Messaging;
using Application.Users.PurgeExpiredRefreshTokens;
using Hangfire;
using Jobs.Abstractions;
using Microsoft.Extensions.Logging;
using SharedKernel.Results;

namespace Jobs.Recurring;

/// <summary>
/// Example recurring job. A job is a thin trigger: the work is a use case (command + handler) in Application,
/// so it is unit-testable and goes through the same validation and logging as a request.
/// </summary>
[AutomaticRetry(Attempts = 3)]
internal sealed class PurgeExpiredRefreshTokensJob(
    ICommandHandler<PurgeExpiredRefreshTokensCommand, int> handler,
    ILogger<PurgeExpiredRefreshTokensJob> logger) : JobBase(logger)
{
    private readonly ILogger<PurgeExpiredRefreshTokensJob> _logger = logger;

    public override string Id => "purge-expired-refresh-tokens";

    public override string Cron => Hangfire.Cron.Daily(3);

    public override string Queue => Queues.Maintenance;

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        CommandResult<int> result = await handler.Handle(new PurgeExpiredRefreshTokensCommand(), cancellationToken);

        EnsureSuccess(result);

        _logger.LogInformation("Deleted {Count} expired refresh token(s)", result.Data);
    }
}
