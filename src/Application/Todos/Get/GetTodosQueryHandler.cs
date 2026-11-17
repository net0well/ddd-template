using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Domain.Todos;
using SharedKernel.Pagination;
using SharedKernel.Results;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryHandler(ITodoItemsSearchableRepository todoItems, IUserContext userContext)
    : IQueryHandler<GetTodosQuery, PagedList<TodoResponse>>
{
    public async Task<CommandResult<PagedList<TodoResponse>>> Handle(
        GetTodosQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = new TodoItemSearchCriteria(
            userContext.UserId,
            query.Search,
            Enum.TryParse(query.SortColumn, ignoreCase: true, out TodoItemSortField sortBy) ? sortBy : TodoItemSortField.CreatedAt,
            string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase),
            query.Page,
            query.PageSize);

        PagedList<TodoItem> page = await todoItems.SearchAsync(criteria, cancellationToken);

        return CommandResult.Ok(page.Map(ToResponse));
    }

    private static TodoResponse ToResponse(TodoItem todoItem) => new()
    {
        Id = todoItem.Id,
        UserId = todoItem.UserId,
        Description = todoItem.Description,
        DueDate = todoItem.DueDate,
        Labels = [.. todoItem.Labels],
        Priority = todoItem.Priority,
        IsCompleted = todoItem.IsCompleted,
        CreatedAt = todoItem.CreatedOnUtc,
        CompletedAt = todoItem.CompletedAt
    };
}
