using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using SharedKernel.Results;

namespace Application.Abstractions.Behaviors;

/// <summary>
/// Wraps handlers of <see cref="ITransactionalCommand"/>s in a transaction; other commands pass through.
/// Innermost decorator, so invalid requests never open a transaction.
/// </summary>
internal static class TransactionDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IUnitOfWork unitOfWork)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<CommandResult<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            command is ITransactionalCommand
                ? unitOfWork.ExecuteInTransactionAsync(
                    token => innerHandler.Handle(command, token),
                    cancellationToken)
                : innerHandler.Handle(command, cancellationToken);
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IUnitOfWork unitOfWork)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<CommandResult> Handle(TCommand command, CancellationToken cancellationToken) =>
            command is ITransactionalCommand
                ? unitOfWork.ExecuteInTransactionAsync(
                    token => innerHandler.Handle(command, token),
                    cancellationToken)
                : innerHandler.Handle(command, cancellationToken);
    }
}
