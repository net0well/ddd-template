using System.Net;

namespace IntegrationTests.Api;

public sealed class ApiDocumentationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Swagger_Should_GenerateTheDocument_WithTheBearerScheme()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("swagger/v1/swagger.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string document = await response.Content.ReadAsStringAsync();
        document.ShouldContain("\"bearer\"");
        document.ShouldContain("CommandResultOfGuid");
        document.ShouldContain("/todos/{id}");
    }

    [Fact]
    public async Task Health_Should_ReportHealthy_WithoutAuthentication()
    {
        // Act
        HttpResponseMessage live = await HttpClient.GetAsync("health/live");
        HttpResponseMessage ready = await HttpClient.GetAsync("health");

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
        HttpResponseMessage response = await HttpClient.GetAsync(path);

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
        HttpResponseMessage response = await HttpClient.GetAsync("todos");

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
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues("Correlation-Id").ShouldHaveSingleItem().ShouldBe("abc-123");
    }
}
