using SharedKernel.Results;

namespace Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<CommandResult> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<CommandResult<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
