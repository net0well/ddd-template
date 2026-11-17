using SharedKernel.Results;

namespace Application.Abstractions.Messaging;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<CommandResult<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
