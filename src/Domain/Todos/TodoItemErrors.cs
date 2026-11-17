using SharedKernel.Notifications;

namespace Domain.Todos;

/// <summary>
/// Notifications of the to-do feature. Keys follow <c>"TodoItems.{Reason}"</c>.
/// </summary>
public static class TodoItemErrors
{
    public static readonly Notification UserRequired = Notification.Validation(
        "TodoItems.UserRequired",
        "A to-do item must belong to a user.");

    public static readonly Notification DescriptionRequired = Notification.Validation(
        "TodoItems.DescriptionRequired",
        "The description is required.");

    public static readonly Notification DescriptionTooLong = Notification.Validation(
        "TodoItems.DescriptionTooLong",
        $"The description must have at most {TodoItem.DescriptionMaxLength} characters.");

    public static readonly Notification InvalidPriority = Notification.Validation(
        "TodoItems.InvalidPriority",
        "The priority is not valid.");

    public static Notification NotFound(Guid todoItemId) => Notification.NotFound(
        "TodoItems.NotFound",
        $"The to-do item with the Id = '{todoItemId}' was not found");

    public static Notification AlreadyCompleted(Guid todoItemId) => Notification.BusinessRule(
        "TodoItems.AlreadyCompleted",
        $"The todo item with Id = '{todoItemId}' is already completed.");
}
