using SharedKernel.Pagination;
using SharedKernel.Persistence;

namespace Domain.Todos;

/// <summary>
/// Read side of the <see cref="TodoItem"/> aggregate. Returns untracked instances — use
/// <see cref="ITodoItemsWritableRepository"/> to load an item you are going to change.
/// </summary>
public interface ITodoItemsSearchableRepository : ISearchableRepository
{
    /// <summary>Returns the item only if it belongs to <paramref name="userId"/>.</summary>
    Task<TodoItem?> GetByIdAsync(Guid todoItemId, Guid userId, CancellationToken cancellationToken = default);

    Task<PagedList<TodoItem>> SearchAsync(TodoItemSearchCriteria criteria, CancellationToken cancellationToken = default);
}
