using System.Net;
using System.Net.Http.Json;
using Application.Users.SendWelcomeEmail;
using Domain.Users;
using Hangfire;
using Jobs;
using Jobs.Commands;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Events;

namespace IntegrationTests.Persistence;

public sealed class DomainEventDispatchTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task FailingHandler_Should_NotFailTheRequest_NorStopTheOtherHandlers()
    {
        // Arrange
        using WebApplicationFactoryWithFailingHandler app = new(Factory);
        HttpClient client = app.CreateClient();
        string email = UniqueEmail();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Event", lastName = "Isolation", password = Password });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        Factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi()
            .EnqueuedJobs(Queues.Default, 0, 1000)
            .ShouldContain(job =>
                job.Value.Job != null &&
                job.Value.Job.Type == typeof(CommandJob<SendWelcomeEmailCommand>) &&
                ((SendWelcomeEmailCommand)job.Value.Job.Args[0]).Email == email);
    }

    private sealed class FailingHandler : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated handler failure.");
    }

    private sealed class WebApplicationFactoryWithFailingHandler(IntegrationTestWebAppFactory parent) : IDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory =
            parent.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, FailingHandler>()));

        public HttpClient CreateClient() => _factory.CreateClient();

        public void Dispose() => _factory.Dispose();
    }
}
