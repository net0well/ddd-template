using Application.Abstractions.Messaging;
using Application.Todos.Update;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Description);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}", async (
            Guid id,
            Request request,
            ICommandHandler<UpdateTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult result = await handler.Handle(new UpdateTodoCommand(id, request.Description), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Todos)
        .ProducesCommandResult()
        .RequireAuthorization();
    }
}
