using Application.Abstractions.Messaging;
using Application.Todos.Delete;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("todos/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult result = await handler.Handle(new DeleteTodoCommand(id), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Todos)
        .ProducesCommandResult()
        .RequireAuthorization();
    }
}
