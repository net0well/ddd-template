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
        .WithName("CopyTodo")
        .WithSummary("Copy a to-do item")
        .WithDescription("Creates a new, not completed item with the same content.")
        .ProducesCommandResult<Guid>(StatusCodes.Status201Created)
        .ProducesErrors(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
