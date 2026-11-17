using Application.Abstractions.Authentication;
using Application.Users;
using Application.Users.Refresh;
using Application.UnitTests.Abstractions;
using Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Results;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);

    private readonly IRefreshTokensWritableRepository _refreshTokens = Substitute.For<IRefreshTokensWritableRepository>();
    private readonly IUsersSearchableRepository _users = Substitute.For<IUsersSearchableRepository>();
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _tokenProvider.HashRefreshToken(Arg.Any<string>()).Returns(call => $"hash-of-{call.Arg<string>()}");
        _tokenProvider.GenerateRefreshToken().Returns("new-token");
        _tokenProvider.Create(Arg.Any<User>()).Returns("new-access-token");
        _tokenProvider.RefreshTokenLifetime.Returns(Lifetime);
        _tokenProvider.RefreshTokenReuseGracePeriod.Returns(GracePeriod);

        _handler = new RefreshTokenCommandHandler(
            _refreshTokens,
            _users,
            UnitOfWork,
            _tokenProvider,
            Clock,
            NullLogger<RefreshTokenCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalid_WhenTokenDoesNotExist()
    {
        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("unknown"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalid_WhenTokenIsExpired()
    {
        // Arrange
        StoredToken("old-token", issuedAt: UtcNow - Lifetime - TimeSpan.FromMinutes(1));

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("old-token"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidRefreshToken);
        _refreshTokens.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_Should_RevokeEverySession_WhenARevokedTokenIsReused()
    {
        // Arrange
        RefreshToken reused = StoredToken("stolen-token", issuedAt: UtcNow.AddHours(-1));
        reused.Revoke(UtcNow.AddMinutes(-30));
        var otherSession = RefreshToken.Issue(reused.UserId, "other", UtcNow.AddMinutes(-10), Lifetime);
        _refreshTokens.GetActiveByUserIdAsync(reused.UserId, UtcNow, Arg.Any<CancellationToken>()).Returns([otherSession]);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("stolen-token"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidRefreshToken);
        otherSession.IsRevoked.ShouldBeTrue();
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_AndKeepTheSession_WhenTheTokenWasJustRotated()
    {
        // Arrange: another tab rotated it five seconds ago
        RefreshToken rotated = StoredToken("tab-token", issuedAt: UtcNow.AddHours(-1));
        rotated.Rotate("hash-of-other-tab", UtcNow.AddSeconds(-5), Lifetime);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("tab-token"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.RefreshTokenConflict);
        await _refreshTokens.DidNotReceiveWithAnyArgs().GetActiveByUserIdAsync(Guid.Empty, UtcNow, CancellationToken.None);
        await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_RevokeEverySession_WhenARotatedTokenIsReusedAfterTheGracePeriod()
    {
        // Arrange
        RefreshToken rotated = StoredToken("old-token", issuedAt: UtcNow.AddHours(-1));
        rotated.Rotate("hash-of-successor", UtcNow.AddSeconds(-5), Lifetime);
        var successor = RefreshToken.Issue(rotated.UserId, "hash-of-successor", UtcNow.AddSeconds(-5), Lifetime);
        _refreshTokens.GetActiveByUserIdAsync(rotated.UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([successor]);
        Clock.Advance(GracePeriod);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("old-token"), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidRefreshToken);
        successor.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_RotateTheToken_WhenValid()
    {
        // Arrange
        RefreshToken current = StoredToken("current-token", issuedAt: UtcNow.AddHours(-1));
        var user = User.Create(Email.Create("john@example.com"), "John", "Doe", "hash");
        _users.GetByIdAsync(current.UserId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(new RefreshTokenCommand("current-token"), CancellationToken.None);

        // Assert
        result.Data.ShouldBe(new AccessTokensResponse("new-access-token", "new-token"));
        current.IsRevoked.ShouldBeTrue();
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.Id == current.ReplacedByTokenId && t.TokenHash == "hash-of-new-token"));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private RefreshToken StoredToken(string value, DateTime issuedAt)
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), $"hash-of-{value}", issuedAt, Lifetime);

        _refreshTokens.GetByTokenHashAsync($"hash-of-{value}", Arg.Any<CancellationToken>()).Returns(token);

        return token;
    }
}
