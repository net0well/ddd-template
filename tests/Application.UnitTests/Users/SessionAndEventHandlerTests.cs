using Application.Abstractions.BackgroundJobs;
using Application.Users.Logout;
using Application.Users.Register;
using Application.Users.SendWelcomeEmail;
using Application.UnitTests.Abstractions;
using Domain.Users;
using SharedKernel.Results;

namespace Application.UnitTests.Users;

public sealed class SessionAndEventHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Logout_Should_RevokeEveryActiveTokenOfTheCurrentUser()
    {
        // Arrange
        IRefreshTokensWritableRepository refreshTokens = Substitute.For<IRefreshTokensWritableRepository>();
        RefreshToken[] sessions =
        [
            RefreshToken.Issue(CurrentUserId, "a", UtcNow, TimeSpan.FromDays(1)),
            RefreshToken.Issue(CurrentUserId, "b", UtcNow, TimeSpan.FromDays(1))
        ];
        refreshTokens.GetActiveByUserIdAsync(CurrentUserId, UtcNow, Arg.Any<CancellationToken>()).Returns(sessions);

        var handler = new LogoutUserCommandHandler(refreshTokens, UnitOfWork, UserContext, DateTimeProvider);

        // Act
        CommandResult result = await handler.Handle(new LogoutUserCommand(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        sessions.ShouldAllBe(token => token.IsRevoked);
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserRegistered_Should_EnqueueTheWelcomeEmail()
    {
        // Arrange
        IJobScheduler jobScheduler = Substitute.For<IJobScheduler>();
        var handler = new UserRegisteredDomainEventHandler(jobScheduler);
        var domainEvent = new UserRegisteredDomainEvent(Guid.CreateVersion7(), "john@example.com");

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        jobScheduler.Received(1).Enqueue(new SendWelcomeEmailCommand(domainEvent.UserId, domainEvent.Email));
    }
}
