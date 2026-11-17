using System.Linq.Expressions;
using Domain.Todos;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DependencyInjection;
using SharedKernel.Pagination;

namespace Infrastructure.Todos;

internal sealed class TodoItemsSearchableRepository(ApplicationDbContext context) : ITodoItemsSearchableRepository, IInjectScoped
{
    public Task<TodoItem?> GetByIdAsync(Guid todoItemId, Guid userId, CancellationToken cancellationToken = default) =>
        context.TodoItems
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == todoItemId && t.UserId == userId, cancellationToken);

    public Task<PagedList<TodoItem>> SearchAsync(TodoItemSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        IQueryable<TodoItem> query = context.TodoItems
            .AsNoTracking()
            .Where(t => t.UserId == criteria.UserId);

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            string pattern = $"%{criteria.Search.Trim().EscapeLikePattern()}%";
            query = query.Where(t => EF.Functions.ILike(t.Description, pattern, @"\"));
        }

        bool descending = criteria.SortDescending;

        IOrderedQueryable<TodoItem> ordered = criteria.SortBy switch
        {
            TodoItemSortField.Description => OrderBy(query, t => t.Description, descending),
            TodoItemSortField.DueDate => OrderBy(query, t => t.DueDate, descending),
            TodoItemSortField.Priority => OrderBy(query, t => t.Priority, descending),
            _ => OrderBy(query, t => t.CreatedOnUtc, descending)
        };

        // Tie-breaker so paging is stable when the sort key repeats.
        return ordered.ThenBy(t => t.Id).ToPagedListAsync(criteria.Page, criteria.PageSize, cancellationToken);
    }

    private static IOrderedQueryable<TodoItem> OrderBy<TKey>(
        IQueryable<TodoItem> query,
        Expression<Func<TodoItem, TKey>> keySelector,
        bool descending) =>
        descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
}
