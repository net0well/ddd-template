using System.Net.Http.Json;
using Application.Users.SendWelcomeEmail;
using Domain.Users;
using Hangfire;
using Infrastructure.Database;
using Infrastructure.Outbox;
using Jobs;
using Jobs.Commands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Events;

namespace IntegrationTests.Outbox;

public sealed class OutboxTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string WelcomeEmailHandler = "Application.Users.Register.UserRegisteredDomainEventHandler";

    [Fact]
    public async Task DomainEvent_Should_BeSavedWithTheChange_AndDeliveredLater_InTheRequestTrace()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        Envelope<Guid> registration = await RegisterAsync(HttpClient, email);
        Guid userId = registration.Data;

        OutboxMessage pending = await GetRegistrationMessageAsync(Factory.Services, userId);
        int enqueuedBeforeDelivery = WelcomeEmailsEnqueued(email);

        await ProcessOutboxAsync();

        OutboxMessage delivered = await GetRegistrationMessageAsync(Factory.Services, userId);

        // Assert
        pending.ProcessedOnUtc.ShouldBeNull();
        pending.TraceParent.ShouldNotBeNull().ShouldContain(registration.TraceId!);
        enqueuedBeforeDelivery.ShouldBe(0);

        delivered.ProcessedOnUtc.ShouldNotBeNull();
        delivered.Attempts.ShouldBe(0);
        WelcomeEmailsEnqueued(email).ShouldBe(1);
    }

    [Fact]
    public async Task FailingHandler_Should_BeRetried_WithoutRepeatingTheHandlersThatSucceeded()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, FailingHandler>()));
        string email = UniqueEmail();
        Guid userId = (await RegisterAsync(app.CreateClient(), email)).Data;

        // Act: first attempt, then the retry once the backoff is over
        await ProcessOutboxAsync(app.Services);
        OutboxMessage afterFirstAttempt = await GetRegistrationMessageAsync(app.Services, userId);

        await MakeDueAsync(afterFirstAttempt.Id);
        await ProcessOutboxAsync(app.Services);
        OutboxMessage afterRetry = await GetRegistrationMessageAsync(app.Services, userId);

        // Assert
        afterFirstAttempt.Attempts.ShouldBe(1);
        afterFirstAttempt.NextAttemptOnUtc.ShouldNotBeNull();

        afterRetry.ProcessedOnUtc.ShouldBeNull();
        afterRetry.Attempts.ShouldBe(2);
        afterRetry.Error.ShouldNotBeNull().ShouldContain("Simulated handler failure");

        (await ConsumersOfAsync(afterRetry.Id)).ShouldBe([WelcomeEmailHandler]);
        WelcomeEmailsEnqueued(email).ShouldBe(1);
    }

    [Fact]
    public async Task FailingHandler_Should_BecomeADeadLetter_AfterTheMaximumAttempts()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IDomainEventHandler<UserRegisteredDomainEvent>, FailingHandler>()));
        int maxAttempts = app.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;
        Guid userId = (await RegisterAsync(app.CreateClient(), UniqueEmail())).Data;

        // Act: one more delivery attempt than allowed
        for (int attempt = 0; attempt <= maxAttempts; attempt++)
        {
            await ProcessOutboxAsync(app.Services);
            await MakeDueAsync((await GetRegistrationMessageAsync(app.Services, userId)).Id);
        }

        // Assert
        OutboxMessage message = await GetRegistrationMessageAsync(app.Services, userId);
        message.Attempts.ShouldBe(maxAttempts);
        message.DeadLetteredOnUtc.ShouldNotBeNull();
        message.ProcessedOnUtc.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownEventType_Should_BecomeADeadLetter_WithoutBlockingTheQueue()
    {
        // Arrange: the oldest pending message, so it is claimed first
        var poison = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = "Domain.Removed.SomethingHappenedDomainEvent",
            Content = "{}",
            OccurredOnUtc = DateTime.UtcNow.AddYears(-1)
        };
        await AddAsync(poison);
        string email = UniqueEmail();
        await RegisterAsync(HttpClient, email);

        // Act
        await ProcessOutboxAsync();

        // Assert
        OutboxMessage deadLetter = await FindAsync(poison.Id);
        deadLetter.ProcessedOnUtc.ShouldBeNull();
        deadLetter.DeadLetteredOnUtc.ShouldNotBeNull();
        deadLetter.Error.ShouldNotBeNull().ShouldContain("Unknown event type");
        WelcomeEmailsEnqueued(email).ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentProcessors_Should_NeverDeliverTheSameMessageTwice()
    {
        // Arrange: small batches so both processors keep claiming while the other one holds rows
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Outbox:BatchSize", "3"));
        string[] emails = [.. Enumerable.Range(0, 12).Select(_ => UniqueEmail())];

        foreach (string email in emails)
        {
            await RegisterAsync(HttpClient, email);
        }

        // Act
        await Task.WhenAll(ProcessOutboxAsync(app.Services), ProcessOutboxAsync(app.Services), ProcessOutboxAsync());

        // Assert
        emails.ShouldAllBe(email => WelcomeEmailsEnqueued(email) == 1);
    }

    [Fact]
    public async Task Cleanup_Should_DeleteOnlyMessagesProcessedBeforeTheRetention()
    {
        // Arrange
        OutboxMessage old = Processed(DateTime.UtcNow.AddDays(-30));
        OutboxMessage recent = Processed(DateTime.UtcNow.AddMinutes(-1));
        await AddAsync(old, recent);

        // Act
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().DeleteProcessedAsync(CancellationToken.None);

        // Assert
        (await ExistsAsync(old.Id)).ShouldBeFalse();
        (await ExistsAsync(recent.Id)).ShouldBeTrue();
    }

    private static OutboxMessage Processed(DateTime processedOnUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = typeof(UserRegisteredDomainEvent).FullName!,
        Content = "{}",
        OccurredOnUtc = processedOnUtc,
        ProcessedOnUtc = processedOnUtc
    };

    private static async Task<Envelope<Guid>> RegisterAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Outbox", lastName = "Test", password = Password });

        response.EnsureSuccessStatusCode();

        return await ReadEnvelopeAsync<Guid>(response);
    }

    private static async Task<OutboxMessage> GetRegistrationMessageAsync(IServiceProvider services, Guid userId)
    {
        using IServiceScope scope = services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.OutboxMessages
            .FromSql($"SELECT * FROM public.outbox_messages WHERE content->>'userId' = {userId.ToString()}")
            .AsNoTracking()
            .SingleAsync();
    }

    private async Task<OutboxMessage> FindAsync(Guid id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id);
    }

    private async Task<bool> ExistsAsync(Guid id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessages.AnyAsync(message => message.Id == id);
    }

    private async Task<List<string>> ConsumersOfAsync(Guid id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessageConsumers
            .Where(consumer => consumer.OutboxMessageId == id)
            .Select(consumer => consumer.Handler)
            .ToListAsync();
    }

    private async Task AddAsync(params OutboxMessage[] messages)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.OutboxMessages.AddRange(messages);
        await db.SaveChangesAsync();
    }

    private async Task MakeDueAsync(Guid id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessages
            .Where(message => message.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.NextAttemptOnUtc, DateTime.UtcNow.AddSeconds(-1)));
    }

    private int WelcomeEmailsEnqueued(string email) =>
        Factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi()
            .EnqueuedJobs(Queues.Default, 0, 10_000)
            .Count(job =>
                job.Value.Job?.Type == typeof(CommandJob<SendWelcomeEmailCommand>) &&
                ((SendWelcomeEmailCommand)job.Value.Job.Args[0]).Email == email);

    private sealed class FailingHandler : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated handler failure.");
    }
}
