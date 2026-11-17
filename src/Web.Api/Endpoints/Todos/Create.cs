using Application.Abstractions.Messaging;
using Application.Todos.Create;
using Domain.Todos;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Description, DateTime? DueDate, List<string>? Labels, Priority Priority);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos", async (
            Request request,
            ICommandHandler<CreateTodoCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTodoCommand(request.Description, request.DueDate, request.Labels ?? [], request.Priority);

            CommandResult<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithTags(Tags.Todos)
        .WithIdempotency()
        .ProducesCommandResult<Guid>()
        .RequireAuthorization();
    }
}
