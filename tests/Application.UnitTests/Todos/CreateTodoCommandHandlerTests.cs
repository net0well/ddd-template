using Application.Todos.Create;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using SharedKernel.Results;

namespace Application.UnitTests.Todos;

public sealed class CreateTodoCommandHandlerTests : BaseHandlerTest
{
    private readonly ITodoItemsWritableRepository _todoItems = Substitute.For<ITodoItemsWritableRepository>();
    private readonly IUsersSearchableRepository _users = Substitute.For<IUsersSearchableRepository>();
    private readonly CreateTodoCommandHandler _handler;

    public CreateTodoCommandHandlerTests()
    {
        _users.ExistsAsync(CurrentUserId, Arg.Any<CancellationToken>()).Returns(true);
        _handler = new CreateTodoCommandHandler(_todoItems, _users, UnitOfWork, UserContext);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _users.ExistsAsync(CurrentUserId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        CommandResult<Guid> result = await _handler.Handle(Command("Write tests"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.NotFound(CurrentUserId));
        _todoItems.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_Should_ReturnDomainNotifications_AndNotPersist_WhenItemIsInvalid()
    {
        // Act
        CommandResult<Guid> result = await _handler.Handle(Command(string.Empty), CancellationToken.None);

        // Assert
        result.Notifications.ShouldContain(TodoItemErrors.DescriptionRequired);
        _todoItems.DidNotReceiveWithAnyArgs().Add(default!);
        await UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_Should_AddAndSaveTheItem_WhenValid()
    {
        // Act
        CommandResult<Guid> result = await _handler.Handle(Command("Write tests"), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _todoItems.Received(1).Add(Arg.Is<TodoItem>(t =>
            t.Id == result.Data && t.UserId == CurrentUserId && t.Description == "Write tests"));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CreateTodoCommand Command(string description) =>
        new(description, null, ["work"], Priority.Medium);
}
