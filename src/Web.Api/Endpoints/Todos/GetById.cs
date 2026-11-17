using Application.Abstractions.Messaging;
using Application.Todos.GetById;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos/{id:guid}", async (
            Guid id,
            IQueryHandler<GetTodoByIdQuery, TodoResponse> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult<TodoResponse> result = await handler.Handle(new GetTodoByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Todos)
        .ProducesCommandResult<TodoResponse>()
        .RequireAuthorization();
    }
}
