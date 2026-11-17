using Application.Todos.Delete;
using Application.Todos.Update;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using SharedKernel.Results;

namespace Application.UnitTests.Todos;

public sealed class UpdateAndDeleteTodoCommandHandlerTests : BaseHandlerTest
{
    private readonly ITodoItemsWritableRepository _todoItems = Substitute.For<ITodoItemsWritableRepository>();

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var handler = new UpdateTodoCommandHandler(_todoItems, UnitOfWork, UserContext, CreateCache());
        var command = new UpdateTodoCommand(Guid.CreateVersion7(), "New");

        // Act
        CommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
    }

    [Fact]
    public async Task Update_Should_ChangeTheDescriptionAndSave_WhenValid()
    {
        // Arrange
        TodoItem todoItem = ExistingTodo();
        var handler = new UpdateTodoCommandHandler(_todoItems, UnitOfWork, UserContext, CreateCache());

        // Act
        CommandResult result = await handler.Handle(new UpdateTodoCommand(todoItem.Id, "Updated"), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        todoItem.Description.ShouldBe("Updated");
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Should_RaiseEventRemoveAndSave_WhenTodoExists()
    {
        // Arrange
        TodoItem todoItem = ExistingTodo();
        var handler = new DeleteTodoCommandHandler(_todoItems, UnitOfWork, UserContext, CreateCache());

        // Act
        CommandResult result = await handler.Handle(new DeleteTodoCommand(todoItem.Id), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        todoItem.DomainEvents.ShouldContain(new TodoItemDeletedDomainEvent(todoItem.Id));
        _todoItems.Received(1).Remove(todoItem);
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TodoItem ExistingTodo()
    {
        var todoItem = TodoItem.Create(CurrentUserId, "Original", Priority.Normal, null, []);

        _todoItems.GetByIdAsync(todoItem.Id, CurrentUserId, Arg.Any<CancellationToken>()).Returns(todoItem);

        return todoItem;
    }
}
