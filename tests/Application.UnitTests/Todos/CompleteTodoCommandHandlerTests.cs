using Application.Todos;
using Application.Todos.Complete;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel.Results;

namespace Application.UnitTests.Todos;

public sealed class CompleteTodoCommandHandlerTests : BaseHandlerTest
{
    private readonly ITodoItemsWritableRepository _todoItems = Substitute.For<ITodoItemsWritableRepository>();
    private readonly HybridCache _cache = CreateCache();
    private readonly CompleteTodoCommandHandler _handler;

    public CompleteTodoCommandHandlerTests()
    {
        _handler = new CompleteTodoCommandHandler(_todoItems, UnitOfWork, UserContext, DateTimeProvider, _cache);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var command = new CompleteTodoCommand(Guid.CreateVersion7());

        // Act
        CommandResult result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
    }

    [Fact]
    public async Task Handle_Should_ReturnBusinessRule_AndNotSave_WhenAlreadyCompleted()
    {
        // Arrange
        TodoItem todoItem = ExistingTodo();
        todoItem.Complete(UtcNow);

        // Act
        CommandResult result = await _handler.Handle(new CompleteTodoCommand(todoItem.Id), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
        await UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_Should_CompleteSaveAndInvalidateCache_WhenValid()
    {
        // Arrange
        TodoItem todoItem = ExistingTodo();
        string cacheKey = TodoCacheKeys.ById(CurrentUserId, todoItem.Id);
        await _cache.SetAsync(cacheKey, "cached");

        // Act
        CommandResult result = await _handler.Handle(new CompleteTodoCommand(todoItem.Id), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.DomainEvents.ShouldContain(new TodoItemCompletedDomainEvent(todoItem.Id));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        (await _cache.GetOrCreateAsync(cacheKey, _ => ValueTask.FromResult("recomputed"))).ShouldBe("recomputed");
    }

    private TodoItem ExistingTodo()
    {
        var todoItem = TodoItem.Create(CurrentUserId, "Finish me", Priority.Normal, null, [], UtcNow);

        _todoItems.GetByIdAsync(todoItem.Id, CurrentUserId, Arg.Any<CancellationToken>()).Returns(todoItem);

        return todoItem;
    }
}
