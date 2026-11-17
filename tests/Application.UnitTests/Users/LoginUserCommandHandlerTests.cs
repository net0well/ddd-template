using Application.Abstractions.Authentication;
using Application.Users;
using Application.Users.Login;
using Application.UnitTests.Abstractions;
using Domain.Users;
using SharedKernel.Results;

namespace Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
    private const string Password = "Password123";

    private readonly IUsersSearchableRepository _users = Substitute.For<IUsersSearchableRepository>();
    private readonly IUsersWritableRepository _writableUsers = Substitute.For<IUsersWritableRepository>();
    private readonly IRefreshTokensWritableRepository _refreshTokens = Substitute.For<IRefreshTokensWritableRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        _tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        _tokenProvider.GenerateRefreshToken().Returns("refresh-token");
        _tokenProvider.HashRefreshToken("refresh-token").Returns("refresh-token-hash");
        _tokenProvider.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));

        _handler = new LoginUserCommandHandler(
            _users, _writableUsers, _refreshTokens, UnitOfWork, _passwordHasher, _tokenProvider, Clock);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_AndStillVerify_WhenUserDoesNotExist()
    {
        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(
            new LoginUserCommand("missing@example.com", Password),
            CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidCredentials);
        _passwordHasher.Received(1).Verify(Password, null);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenPasswordIsWrong()
    {
        // Arrange
        ExistingUser();
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(PasswordVerificationResult.Failed);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(
            new LoginUserCommand("john@example.com", "wrong"),
            CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.InvalidCredentials);
        _refreshTokens.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_Should_StoreOnlyTheRefreshTokenHash_AndReturnTokens()
    {
        // Arrange
        User user = ExistingUser();
        _passwordHasher.Verify(Password, user.PasswordHash).Returns(PasswordVerificationResult.Success);

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(
            new LoginUserCommand("John@Example.com", Password),
            CancellationToken.None);

        // Assert
        result.Data.ShouldBe(new AccessTokensResponse("access-token", "refresh-token"));
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t =>
            t.UserId == user.Id && t.TokenHash == "refresh-token-hash" && t.ExpiresOnUtc == UtcNow.AddDays(7)));
        await _writableUsers.DidNotReceiveWithAnyArgs().GetByIdAsync(Guid.Empty, CancellationToken.None);
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_UpgradeTheStoredHash_WhenRehashIsNeeded()
    {
        // Arrange
        User user = ExistingUser();
        var tracked = User.Create(user.Email, "John", "Doe", user.PasswordHash);
        _writableUsers.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(tracked);
        _passwordHasher.Verify(Password, user.PasswordHash).Returns(PasswordVerificationResult.SuccessRehashNeeded);
        _passwordHasher.Hash(Password).Returns("new-hash");

        // Act
        CommandResult<AccessTokensResponse> result = await _handler.Handle(
            new LoginUserCommand("john@example.com", Password),
            CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        tracked.PasswordHash.ShouldBe("new-hash");
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private User ExistingUser()
    {
        var user = User.Create(Email.Create("john@example.com"), "John", "Doe", "stored-hash");

        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);

        return user;
    }
}
