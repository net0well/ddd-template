using Application.Abstractions.Messaging;
using SharedKernel.Pagination;

namespace Application.Todos.Get;

public sealed record GetTodosQuery(
    string? Search,
    string? SortColumn,
    string? SortOrder,
    int Page,
    int PageSize) : IQuery<PagedList<TodoResponse>>;
