namespace Domain.Todos;

/// <summary>
/// What a to-do search filters, sorts and pages by. Always scoped to one user.
/// </summary>
public sealed record TodoItemSearchCriteria(
    Guid UserId,
    string? Search,
    TodoItemSortField SortBy,
    bool SortDescending,
    int Page,
    int PageSize);
