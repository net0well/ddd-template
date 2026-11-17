using Application.Abstractions.Messaging;
using Application.Todos.Copy;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Copy : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos/{id:guid}/copy", async (
            Guid id,
            ICommandHandler<CopyTodoCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult<Guid> result = await handler.Handle(new CopyTodoCommand(id), cancellationToken);

            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithTags(Tags.Todos)
        .WithIdempotency()
        .ProducesCommandResult<Guid>()
        .RequireAuthorization();
    }
}
