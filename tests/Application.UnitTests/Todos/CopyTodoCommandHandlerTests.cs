using Application.Todos.Copy;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using SharedKernel.Results;

namespace Application.UnitTests.Todos;

public sealed class CopyTodoCommandHandlerTests : BaseHandlerTest
{
    private readonly ITodoItemsSearchableRepository _searchable = Substitute.For<ITodoItemsSearchableRepository>();
    private readonly ITodoItemsWritableRepository _writable = Substitute.For<ITodoItemsWritableRepository>();
    private readonly CopyTodoCommandHandler _handler;

    public CopyTodoCommandHandlerTests()
    {
        _handler = new CopyTodoCommandHandler(_searchable, _writable, UnitOfWork, UserContext);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSourceDoesNotBelongToTheUser()
    {
        // Arrange
        var command = new CopyTodoCommand(Guid.CreateVersion7());

        // Act
        CommandResult<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
    }

    [Fact]
    public async Task Handle_Should_AddANewItemThroughTheWritableSide()
    {
        // Arrange
        var source = TodoItem.Create(CurrentUserId, "Copy me", Priority.High, null, ["a"]);
        _searchable.GetByIdAsync(source.Id, CurrentUserId, Arg.Any<CancellationToken>()).Returns(source);

        // Act
        CommandResult<Guid> result = await _handler.Handle(new CopyTodoCommand(source.Id), CancellationToken.None);

        // Assert
        result.Data.ShouldNotBe(source.Id);
        _writable.Received(1).Add(Arg.Is<TodoItem>(t => t.Id == result.Data && t.Description == "Copy me"));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
