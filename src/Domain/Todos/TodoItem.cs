using SharedKernel.Notifications;
using SharedKernel.Primitives;

namespace Domain.Todos;

public sealed class TodoItem : AggregateRoot
{
    public const int DescriptionMaxLength = 500;

    private readonly List<string> _labels = [];

    private TodoItem()
    {
    }

    public Guid UserId { get; private set; }

    public string Description { get; private set; }

    public DateTime? DueDate { get; private set; }

    public IReadOnlyList<string> Labels => _labels.AsReadOnly();

    public bool IsCompleted { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Priority Priority { get; private set; }

    /// <summary>
    /// Creates the item. Check <see cref="Notifiable.IsValid"/> before persisting it.
    /// </summary>
    public static TodoItem Create(
        Guid userId,
        string? description,
        Priority priority,
        DateTime? dueDate,
        IEnumerable<string>? labels,
        DateTime utcNow)
    {
        var todoItem = new TodoItem
        {
            UserId = userId,
            Description = description?.Trim() ?? string.Empty,
            Priority = priority,
            DueDate = dueDate,
            IsCompleted = false,
            CreatedAt = utcNow
        };

        todoItem._labels.AddRange(labels ?? []);

        if (userId == Guid.Empty)
        {
            todoItem.AddNotification(TodoItemErrors.UserRequired);
        }

        if (!Enum.IsDefined(priority))
        {
            todoItem.AddNotification(TodoItemErrors.InvalidPriority);
        }

        todoItem.CheckDescription(todoItem.Description);

        if (todoItem.IsValid)
        {
            todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));
        }

        return todoItem;
    }

    /// <summary>A new, not completed item for the same user with the same content.</summary>
    public TodoItem Copy(DateTime utcNow) =>
        Create(UserId, Description, Priority, DueDate, Labels, utcNow);

    public void Complete(DateTime utcNow)
    {
        if (IsCompleted)
        {
            AddNotification(TodoItemErrors.AlreadyCompleted(Id));
            return;
        }

        IsCompleted = true;
        CompletedAt = utcNow;

        Raise(new TodoItemCompletedDomainEvent(Id));
    }

    public void UpdateDescription(string? description)
    {
        string newDescription = description?.Trim() ?? string.Empty;

        if (!CheckDescription(newDescription) || newDescription == Description)
        {
            return;
        }

        Description = newDescription;

        Raise(new TodoItemUpdatedDomainEvent(Id));
    }

    /// <summary>Raises the deletion event; the repository removes the item.</summary>
    public void Delete() => Raise(new TodoItemDeletedDomainEvent(Id));

    private bool CheckDescription(string description)
    {
        if (description.Length == 0)
        {
            AddNotification(TodoItemErrors.DescriptionRequired);
            return false;
        }

        if (description.Length > DescriptionMaxLength)
        {
            AddNotification(TodoItemErrors.DescriptionTooLong);
            return false;
        }

        return true;
    }
}
