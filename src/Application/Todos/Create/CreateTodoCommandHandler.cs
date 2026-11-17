using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Domain.Users;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Todos.Create;

internal sealed class CreateTodoCommandHandler(
    ITodoItemsWritableRepository todoItems,
    IUsersSearchableRepository users,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTodoCommand, Guid>
{
    public async Task<CommandResult<Guid>> Handle(CreateTodoCommand command, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        if (!await users.ExistsAsync(userId, cancellationToken))
        {
            return CommandResult.Fail<Guid>(UserErrors.NotFound(userId));
        }

        var todoItem = TodoItem.Create(
            userId,
            command.Description,
            command.Priority,
            command.DueDate,
            command.Labels,
            dateTimeProvider.UtcNow);

        if (!todoItem.IsValid)
        {
            return CommandResult.Fail<Guid>(todoItem);
        }

        todoItems.Add(todoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok(todoItem.Id);
    }
}
