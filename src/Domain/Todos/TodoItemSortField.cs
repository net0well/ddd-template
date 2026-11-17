namespace Domain.Todos;

/// <summary>The fields a to-do search can be ordered by.</summary>
public enum TodoItemSortField
{
    CreatedAt = 0,
    Description = 1,
    DueDate = 2,
    Priority = 3
}
