using Application.Abstractions.Messaging;
using Hangfire;
using Jobs.Abstractions;
using SharedKernel.Results;

namespace Jobs.Commands;

/// <summary>
/// Runs a command enqueued through <c>IJobScheduler</c> with its regular (decorated) handler.
/// </summary>
public sealed class CommandJob<TCommand>(ICommandHandler<TCommand> handler)
    where TCommand : ICommand
{
    [AutomaticRetry(Attempts = 5)]
    public async Task ExecuteAsync(TCommand command, CancellationToken cancellationToken)
    {
        CommandResult result = await handler.Handle(command, cancellationToken);

        result.ThrowIfFailed(typeof(TCommand).Name);
    }
}
