using Application.Abstractions.Messaging;
using Application.Todos.Get;
using SharedKernel.Pagination;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Todos;

internal sealed class Get : IEndpoint
{
    private const int DefaultPageSize = 10;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos", async (
            string? search,
            string? sortColumn,
            string? sortOrder,
            int? page,
            int? pageSize,
            IQueryHandler<GetTodosQuery, PagedList<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTodosQuery(search, sortColumn, sortOrder, page ?? 1, pageSize ?? DefaultPageSize);

            CommandResult<PagedList<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Todos)
        .ProducesCommandResult<PagedList<TodoResponse>>()
        .RequireAuthorization();
    }
}
