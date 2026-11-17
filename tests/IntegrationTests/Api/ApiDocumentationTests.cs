using System.Net;
using System.Text.Json;

namespace IntegrationTests.Api;

public sealed class ApiDocumentationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Swagger_Should_GenerateTheDocument_WithTheBearerScheme()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement paths = document.RootElement.GetProperty("paths");

        JsonElement createTodo = paths.GetProperty("/todos").GetProperty("post");
        createTodo.GetProperty("operationId").GetString().ShouldBe("CreateTodo");
        createTodo.GetProperty("responses").EnumerateObject().Select(r => r.Name)
            .ShouldBe(["201", "400", "404", "401", "409", "500"], ignoreOrder: true);
        createTodo.GetProperty("security").GetArrayLength().ShouldBe(1);
        createTodo.GetProperty("parameters").EnumerateArray()
            .ShouldContain(p => p.GetProperty("name").GetString() == "Idempotency-Key");

        JsonElement login = paths.GetProperty("/users/login").GetProperty("post");
        login.TryGetProperty("security", out _).ShouldBeFalse();
        login.GetProperty("responses").TryGetProperty("429", out _).ShouldBeTrue();

        paths.GetProperty("/users/{userId}").GetProperty("get").GetProperty("responses")
            .TryGetProperty("403", out _).ShouldBeTrue();

        document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CommandResultOfGuid")
            .GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .ShouldBe(["success", "data", "notifications", "traceId"]);
    }

    [Fact]
    public async Task Health_Should_ReportHealthy_WithoutAuthentication()
    {
        // Act
        HttpResponseMessage live = await HttpClient.GetAsync("health/live", TestContext.Current.CancellationToken);
        HttpResponseMessage ready = await HttpClient.GetAsync("health", TestContext.Current.CancellationToken);

        // Assert
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("todos", HttpStatusCode.Unauthorized, "Request.Unauthorized")]
    [InlineData("this-route-does-not-exist", HttpStatusCode.NotFound, "Request.NotFound")]
    public async Task FrameworkErrors_Should_UseTheStandardEnvelope(string path, HttpStatusCode status, string key)
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(status);
        Envelope<object> envelope = await ReadEnvelopeAsync<object>(response);
        envelope.Success.ShouldBeFalse();
        envelope.Notifications.ShouldHaveSingleItem().Key.ShouldBe(key);
    }

    [Fact]
    public async Task Envelope_Should_CarryTheTraceIdOfTheRequest_ForLogLookup()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("todos", TestContext.Current.CancellationToken);

        // Assert
        Envelope<object> envelope = await ReadEnvelopeAsync<object>(response);
        envelope.TraceId.ShouldNotBeNull();
        envelope.TraceId.Length.ShouldBe(32);

        // Without a client Correlation-Id, the correlation id in the logs is the trace id itself.
        response.Headers.GetValues("Correlation-Id").ShouldHaveSingleItem().ShouldBe(envelope.TraceId);
    }

    [Fact]
    public async Task Responses_Should_EchoTheCorrelationId()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "health/live");
        request.Headers.Add("Correlation-Id", "abc-123");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.Headers.GetValues("Correlation-Id").ShouldHaveSingleItem().ShouldBe("abc-123");
    }
}
