using Domain.Todos;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Todos;

internal sealed class TodoItemsWritableRepository(ApplicationDbContext context) : ITodoItemsWritableRepository, IInjectScoped
{
    public Task<TodoItem?> GetByIdAsync(Guid todoItemId, Guid userId, CancellationToken cancellationToken = default) =>
        context.TodoItems.SingleOrDefaultAsync(t => t.Id == todoItemId && t.UserId == userId, cancellationToken);

    public void Add(TodoItem todoItem) => context.TodoItems.Add(todoItem);

    public void Remove(TodoItem todoItem) => context.TodoItems.Remove(todoItem);
}
