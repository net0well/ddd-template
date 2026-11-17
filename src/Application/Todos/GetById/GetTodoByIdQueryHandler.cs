using Application.Abstractions.Authentication;
using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel.Results;

namespace Application.Todos.GetById;

internal sealed class GetTodoByIdQueryHandler(
    ITodoItemsSearchableRepository todoItems,
    IUserContext userContext,
    HybridCache cache)
    : IQueryHandler<GetTodoByIdQuery, TodoResponse>
{
    public async Task<CommandResult<TodoResponse>> Handle(GetTodoByIdQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        TodoResponse? todo = await cache.GetOrCreateWithTraceAsync(
            TodoCacheKeys.ById(userId, query.TodoItemId),
            async cancellation =>
            {
                TodoItem? todoItem = await todoItems.GetByIdAsync(query.TodoItemId, userId, cancellation);

                return todoItem is null ? null : ToResponse(todoItem);
            },
            cancellationToken: cancellationToken);

        return todo is null
            ? CommandResult.Fail<TodoResponse>(TodoItemErrors.NotFound(query.TodoItemId))
            : CommandResult.Ok(todo);
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
