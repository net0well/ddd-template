using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] IntegrationLabels = ["integration"];

    private sealed record TodoDto(Guid Id, Guid UserId, string Description, bool IsCompleted, string Priority);

    private sealed record PagedTodos(
        List<TodoDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        bool HasPreviousPage);

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodo_ThatCanBeRetrievedById()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "todos",
            new { description = "Integration test todo", labels = IntegrationLabels, priority = "High" }, TestContext.Current.CancellationToken);

        // Assert
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid todoId = await ReadDataAsync<Guid>(createResponse);
        todoId.Version.ShouldBe(7);

        TodoDto todo = await ReadDataAsync<TodoDto>(await HttpClient.GetAsync($"todos/{todoId}", TestContext.Current.CancellationToken));
        todo.Id.ShouldBe(todoId);
        todo.UserId.ShouldBe(userId);
        todo.Description.ShouldBe("Integration test todo");
        todo.Priority.ShouldBe("High");
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnValidationNotificationsKeyedByProperty()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("todos", new { description = "", priority = 1 }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Envelope<Guid?> envelope = await ReadEnvelopeAsync<Guid?>(response);
        envelope.Success.ShouldBeFalse();
        envelope.Notifications.ShouldContain(n => n.Key == "Description" && n.Type == "Validation");
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnBadRequestEnvelope_WhenJsonIsMalformed()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        using var malformedJson = new StringContent("{ not json", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync("todos", malformedJson, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadEnvelopeAsync<Guid?>(response)).Notifications.ShouldHaveSingleItem().Key.ShouldBe("Request.Invalid");
    }

    [Fact]
    public async Task CompleteTodo_Should_MarkTodoAsCompleted_AndRejectASecondCompletion()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid todoId = await CreateTodoAsync("Todo to complete");

        // Act
        HttpResponseMessage first = await HttpClient.PutAsync($"todos/{todoId}/complete", null, TestContext.Current.CancellationToken);
        HttpResponseMessage second = await HttpClient.PutAsync($"todos/{todoId}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadEnvelopeAsync<object>(second)).Notifications.ShouldHaveSingleItem().Key.ShouldBe("TodoItems.AlreadyCompleted");

        TodoDto todo = await ReadDataAsync<TodoDto>(await HttpClient.GetAsync($"todos/{todoId}", TestContext.Current.CancellationToken));
        todo.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodo_Should_ReturnNotFound_ForAnotherUsersTodo()
    {
        // Arrange
        (_, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);
        Guid todoId = await CreateTodoAsync("Private todo");

        (_, AccessTokens stranger) = await RegisterAndLoginAsync();
        Authenticate(stranger.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{todoId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnConflict_WhenIdempotencyKeyIsReused()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        string idempotencyKey = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage first = await PostTodoAsync(new { description = "Idempotent", priority = 1 }, idempotencyKey);
        HttpResponseMessage second = await PostTodoAsync(new { description = "Idempotent", priority = 1 }, idempotencyKey);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTodo_Should_ReleaseTheIdempotencyKey_WhenTheRequestFails()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        string idempotencyKey = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage invalid = await PostTodoAsync(new { description = "", priority = 1 }, idempotencyKey);
        HttpResponseMessage retry = await PostTodoAsync(new { description = "Fixed", priority = 1 }, idempotencyKey);

        // Assert
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTodos_Should_ReturnPagedResultsOfTheCurrentUser()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        await CreateTodoAsync("Paged todo 1");
        await CreateTodoAsync("Paged todo 2");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("todos?page=1&pageSize=1&search=paged&sortColumn=description", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTodos page = await ReadDataAsync<PagedTodos>(response);
        page.Items.ShouldHaveSingleItem().Description.ShouldBe("Paged todo 1");
        page.TotalCount.ShouldBe(2);
        page.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodos_Should_ReturnBadRequest_WhenPageSizeIsOutOfRange()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("todos?page=0&pageSize=1000", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateTodoAsync(string description)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("todos", new { description, priority = 1 });

        response.EnsureSuccessStatusCode();

        return await ReadDataAsync<Guid>(response);
    }

    private async Task<HttpResponseMessage> PostTodoAsync(object request, string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "todos")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        return await HttpClient.SendAsync(message);
    }
}
