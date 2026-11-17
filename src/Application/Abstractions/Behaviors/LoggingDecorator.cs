using System.Diagnostics;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Application.Abstractions.Behaviors;

/// <summary>
/// Logs outcome and duration of every command and query: success as Information, notifications as Warning
/// (Error when one is a Failure). Exceptions are logged by the API's global exception handler.
/// </summary>
internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<CommandResult<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            long startedAt = Stopwatch.GetTimestamp();

            CommandResult<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            LogOutcome(logger, typeof(TCommand).Name, result, Stopwatch.GetElapsedTime(startedAt));

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<CommandResult> Handle(TCommand command, CancellationToken cancellationToken)
        {
            long startedAt = Stopwatch.GetTimestamp();

            CommandResult result = await innerHandler.Handle(command, cancellationToken);

            LogOutcome(logger, typeof(TCommand).Name, result, Stopwatch.GetElapsedTime(startedAt));

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<CommandResult<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            long startedAt = Stopwatch.GetTimestamp();

            CommandResult<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            LogOutcome(logger, typeof(TQuery).Name, result, Stopwatch.GetElapsedTime(startedAt));

            return result;
        }
    }

    private static void LogOutcome(ILogger logger, string requestName, CommandResult result, TimeSpan elapsed)
    {
        if (result.Success)
        {
            logger.LogInformation(
                "{RequestName} succeeded in {ElapsedMilliseconds:0.0} ms",
                requestName,
                elapsed.TotalMilliseconds);

            return;
        }

        LogLevel level = result.Notifications.Any(notification => notification.Type == NotificationType.Failure)
            ? LogLevel.Error
            : LogLevel.Warning;

        logger.Log(
            level,
            "{RequestName} completed with {NotificationCount} notification(s) in {ElapsedMilliseconds:0.0} ms: {@Notifications}",
            requestName,
            result.Notifications.Count,
            elapsed.TotalMilliseconds,
            result.Notifications);
    }
}
