using Application.Abstractions.Data;
using Application.Users.SendWelcomeEmail;
using Domain.Todos;
using Domain.Users;
using Hangfire;
using Jobs;
using Jobs.Commands;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace IntegrationTests.Persistence;

public sealed class UnitOfWorkTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ExecuteInTransaction_Should_RollBackAndDiscardEvents_WhenTheResultFails()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        CommandResult result = await RegisterInTransactionAsync(email, fail: true);

        // Assert
        result.Success.ShouldBeFalse();
        (await UserExistsAsync(email)).ShouldBeFalse();
        WelcomeEmailWasEnqueued(email).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteInTransaction_Should_CommitAndDispatchEvents_WhenTheResultSucceeds()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        CommandResult result = await RegisterInTransactionAsync(email, fail: false);

        // Assert
        result.Success.ShouldBeTrue();
        (await UserExistsAsync(email)).ShouldBeTrue();
        WelcomeEmailWasEnqueued(email).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChanges_Should_RefuseToPersistAnInvalidAggregate()
    {
        // Arrange
        using IServiceScope scope = Factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var invalid = TodoItem.Create(Guid.CreateVersion7(), string.Empty, Priority.Low, null, [], DateTime.UtcNow);
        scope.ServiceProvider.GetRequiredService<ITodoItemsWritableRepository>().Add(invalid);

        // Act & Assert
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => unitOfWork.SaveChangesAsync());
        exception.Message.ShouldContain("TodoItems.DescriptionRequired");
    }

    private async Task<CommandResult> RegisterInTransactionAsync(string email, bool fail)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IUsersWritableRepository users = scope.ServiceProvider.GetRequiredService<IUsersWritableRepository>();

        return await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
            users.Add(User.Create(Email.Create(email), "Unit", "OfWork", "hash"));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return fail
                ? CommandResult.Fail(Notification.BusinessRule("Test.ForcedFailure", "Rolls the transaction back."))
                : CommandResult.Ok();
        });
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IUsersSearchableRepository>()
            .ExistsByEmailAsync(Email.Create(email));
    }

    private bool WelcomeEmailWasEnqueued(string email) =>
        Factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi()
            .EnqueuedJobs(Queues.Default, 0, 1000)
            .Any(job =>
                job.Value.Job?.Type == typeof(CommandJob<SendWelcomeEmailCommand>) &&
                ((SendWelcomeEmailCommand)job.Value.Job.Args[0]).Email == email);
}
