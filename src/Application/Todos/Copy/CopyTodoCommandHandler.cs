using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using SharedKernel.Results;

namespace Application.Todos.Copy;

internal sealed class CopyTodoCommandHandler(
    ITodoItemsSearchableRepository todoItemsSearchable,
    ITodoItemsWritableRepository todoItemsWritable,
    IUnitOfWork unitOfWork,
    IUserContext userContext)
    : ICommandHandler<CopyTodoCommand, Guid>
{
    public async Task<CommandResult<Guid>> Handle(CopyTodoCommand command, CancellationToken cancellationToken)
    {
        // The source is only read, so it comes from the searchable side (untracked).
        TodoItem? source = await todoItemsSearchable.GetByIdAsync(
            command.TodoItemId,
            userContext.UserId,
            cancellationToken);

        if (source is null)
        {
            return CommandResult.Fail<Guid>(TodoItemErrors.NotFound(command.TodoItemId));
        }

        TodoItem copy = source.Copy();

        if (!copy.IsValid)
        {
            return CommandResult.Fail<Guid>(copy);
        }

        todoItemsWritable.Add(copy);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok(copy.Id);
    }
}
