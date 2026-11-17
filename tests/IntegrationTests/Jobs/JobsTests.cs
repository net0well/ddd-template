using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Application.Users.SendWelcomeEmail;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Jobs;
using Jobs.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Jobs;

public sealed class JobsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Register_Should_EnqueueTheWelcomeEmail_AsADeserializableJob()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        await RegisterUserAsync(email);
        await ProcessOutboxAsync();

        // Assert
        IMonitoringApi monitoring = Factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        JobList<EnqueuedJobDto> enqueued = monitoring.EnqueuedJobs(Queues.Default, 0, 1000);

        enqueued.ShouldContain(job =>
            job.Value.Job != null &&
            job.Value.Job.Type == typeof(CommandJob<SendWelcomeEmailCommand>) &&
            ((SendWelcomeEmailCommand)job.Value.Job.Args[0]).Email == email);
    }

    [Fact]
    public async Task Dashboard_Should_AskForCredentials_WhenTheyAreMissingOrWrong()
    {
        // Act
        HttpResponseMessage anonymous = await HttpClient.GetAsync("hangfire", TestContext.Current.CancellationToken);
        HttpResponseMessage wrong = await GetDashboardAsync(IntegrationTestWebAppFactory.DashboardUsername, "wrong-password");

        // Assert
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        anonymous.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Basic");
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_Should_Open_WithTheConfiguredCredentials()
    {
        // Act
        HttpResponseMessage response = await GetDashboardAsync(
            IntegrationTestWebAppFactory.DashboardUsername,
            IntegrationTestWebAppFactory.DashboardPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> GetDashboardAsync(string username, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "hangfire");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

        return await HttpClient.SendAsync(request);
    }
}
