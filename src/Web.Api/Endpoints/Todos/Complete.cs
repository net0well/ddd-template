using Application.Abstractions.Messaging;
using Application.Todos.Complete;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Complete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}/complete", async (
            Guid id,
            ICommandHandler<CompleteTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult result = await handler.Handle(new CompleteTodoCommand(id), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Todos)
        .ProducesCommandResult()
        .RequireAuthorization();
    }
}
