using SharedKernel.Persistence;

namespace Domain.Todos;

/// <summary>
/// Write side of the <see cref="TodoItem"/> aggregate. Changes are committed by the unit of work.
/// </summary>
public interface ITodoItemsWritableRepository : IWritableRepository
{
    /// <summary>Loads the item, tracked for change, only if it belongs to <paramref name="userId"/>.</summary>
    Task<TodoItem?> GetByIdAsync(Guid todoItemId, Guid userId, CancellationToken cancellationToken = default);

    void Add(TodoItem todoItem);

    void Remove(TodoItem todoItem);
}
