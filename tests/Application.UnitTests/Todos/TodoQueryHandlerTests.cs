using Application.Todos.Get;
using Application.Todos.GetById;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using SharedKernel.Pagination;
using SharedKernel.Results;

namespace Application.UnitTests.Todos;

public sealed class TodoQueryHandlerTests : BaseHandlerTest
{
    private readonly ITodoItemsSearchableRepository _todoItems = Substitute.For<ITodoItemsSearchableRepository>();

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        var handler = new GetTodoByIdQueryHandler(_todoItems, UserContext, CreateCache());
        var query = new GetTodoByIdQuery(Guid.CreateVersion7());

        // Act
        CommandResult<Application.Todos.GetById.TodoResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(TodoItemErrors.NotFound(query.TodoItemId));
    }

    [Fact]
    public async Task GetById_Should_ServeTheSecondCallFromCache()
    {
        // Arrange
        var todoItem = TodoItem.Create(CurrentUserId, "Cached", Priority.Low, null, []);
        _todoItems.GetByIdAsync(todoItem.Id, CurrentUserId, Arg.Any<CancellationToken>()).Returns(todoItem);
        var handler = new GetTodoByIdQueryHandler(_todoItems, UserContext, CreateCache());

        // Act
        await handler.Handle(new GetTodoByIdQuery(todoItem.Id), CancellationToken.None);
        CommandResult<Application.Todos.GetById.TodoResponse> result =
            await handler.Handle(new GetTodoByIdQuery(todoItem.Id), CancellationToken.None);

        // Assert
        result.Data!.Description.ShouldBe("Cached");
        await _todoItems.Received(1).GetByIdAsync(todoItem.Id, CurrentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_Should_ScopeTheSearchToTheCurrentUser_AndMapThePage()
    {
        // Arrange
        var todoItem = TodoItem.Create(CurrentUserId, "Listed", Priority.Top, null, []);
        _todoItems.SearchAsync(Arg.Any<TodoItemSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedList<TodoItem>([todoItem], 1, 10, 1));
        var handler = new GetTodosQueryHandler(_todoItems, UserContext);

        // Act
        CommandResult<PagedList<Application.Todos.Get.TodoResponse>> result = await handler.Handle(
            new GetTodosQuery("list", "priority", "desc", 1, 10),
            CancellationToken.None);

        // Assert
        result.Data!.Items.ShouldHaveSingleItem().Priority.ShouldBe(Priority.Top);
        await _todoItems.Received(1).SearchAsync(
            new TodoItemSearchCriteria(CurrentUserId, "list", TodoItemSortField.Priority, true, 1, 10),
            Arg.Any<CancellationToken>());
    }
}
