using Domain.Todos;

namespace Domain.UnitTests.Todos;

public sealed class TodoItemTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_BuildAValidItemWithAVersion7Id_AndRaiseCreatedEvent()
    {
        // Act
        var todoItem = TodoItem.Create(UserId, "  Write tests  ", Priority.High, null, ["work"]);

        // Assert
        todoItem.IsValid.ShouldBeTrue();
        todoItem.Id.Version.ShouldBe(7);
        todoItem.Description.ShouldBe("Write tests");
        todoItem.IsCompleted.ShouldBeFalse();
        todoItem.DomainEvents.ShouldHaveSingleItem().ShouldBe(new TodoItemCreatedDomainEvent(todoItem.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Should_NotifyAndRaiseNoEvent_WhenDescriptionIsMissing(string? description)
    {
        // Act
        var todoItem = TodoItem.Create(UserId, description, Priority.Low, null, []);

        // Assert
        todoItem.IsValid.ShouldBeFalse();
        todoItem.Notifications.ShouldContain(TodoItemErrors.DescriptionRequired);
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Should_CollectEveryBrokenRule()
    {
        // Act
        var todoItem = TodoItem.Create(
            Guid.Empty,
            new string('a', TodoItem.DescriptionMaxLength + 1),
            (Priority)99,
            null,
            []);

        // Assert
        todoItem.Notifications.ShouldBe(
            [TodoItemErrors.UserRequired, TodoItemErrors.InvalidPriority, TodoItemErrors.DescriptionTooLong],
            ignoreOrder: true);
    }

    [Fact]
    public void Complete_Should_MarkAsCompletedAndRaiseEvent()
    {
        // Arrange
        TodoItem todoItem = CreateValid();

        // Act
        todoItem.Complete(UtcNow);

        // Assert
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.CompletedAt.ShouldBe(UtcNow);
        todoItem.DomainEvents.ShouldContain(new TodoItemCompletedDomainEvent(todoItem.Id));
    }

    [Fact]
    public void Complete_Should_Notify_WhenAlreadyCompleted()
    {
        // Arrange
        TodoItem todoItem = CreateValid();
        todoItem.Complete(UtcNow);

        // Act
        todoItem.Complete(UtcNow.AddHours(1));

        // Assert
        todoItem.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
        todoItem.CompletedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void UpdateDescription_Should_NotRaiseEvent_WhenDescriptionDidNotChange()
    {
        // Arrange
        TodoItem todoItem = CreateValid();
        todoItem.ClearDomainEvents();

        // Act
        todoItem.UpdateDescription(todoItem.Description);

        // Assert
        todoItem.IsValid.ShouldBeTrue();
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateDescription_Should_KeepTheOldValue_WhenTheNewOneIsInvalid()
    {
        // Arrange
        TodoItem todoItem = CreateValid();

        // Act
        todoItem.UpdateDescription(string.Empty);

        // Assert
        todoItem.Notifications.ShouldContain(TodoItemErrors.DescriptionRequired);
        todoItem.Description.ShouldBe("Original");
    }

    [Fact]
    public void Copy_Should_CreateANewNotCompletedItemWithTheSameContent()
    {
        // Arrange
        TodoItem original = CreateValid();
        original.Complete(UtcNow);

        // Act
        TodoItem copy = original.Copy();

        // Assert
        copy.Id.ShouldNotBe(original.Id);
        copy.IsCompleted.ShouldBeFalse();
        copy.Description.ShouldBe(original.Description);
        copy.Labels.ShouldBe(original.Labels);
        copy.Labels.ShouldNotBeSameAs(original.Labels);
    }

    private static TodoItem CreateValid() =>
        TodoItem.Create(UserId, "Original", Priority.Medium, null, ["label"]);
}
