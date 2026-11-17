using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    ITodoItemsWritableRepository todoItems,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache)
    : ICommandHandler<CompleteTodoCommand>
{
    public async Task<CommandResult> Handle(CompleteTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await todoItems.GetByIdAsync(command.TodoItemId, userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return CommandResult.Fail(TodoItemErrors.NotFound(command.TodoItemId));
        }

        todoItem.Complete(dateTimeProvider.UtcNow);

        if (!todoItem.IsValid)
        {
            return CommandResult.Fail(todoItem);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return CommandResult.Ok();
    }
}
