using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel.Results;

namespace Application.Todos.Delete;

internal sealed class DeleteTodoCommandHandler(
    ITodoItemsWritableRepository todoItems,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<DeleteTodoCommand>
{
    public async Task<CommandResult> Handle(DeleteTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await todoItems.GetByIdAsync(command.TodoItemId, userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return CommandResult.Fail(TodoItemErrors.NotFound(command.TodoItemId));
        }

        todoItem.Delete();
        todoItems.Remove(todoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return CommandResult.Ok();
    }
}
