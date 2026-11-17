using Application.Abstractions.Authentication;
using Application.Users.Register;
using Application.UnitTests.Abstractions;
using Domain.Users;
using SharedKernel.Results;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private readonly IUsersSearchableRepository _usersSearchable = Substitute.For<IUsersSearchableRepository>();
    private readonly IUsersWritableRepository _usersWritable = Substitute.For<IUsersWritableRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        _handler = new RegisterUserCommandHandler(
            new UserRegistrationService(_usersSearchable),
            _usersWritable,
            UnitOfWork,
            _passwordHasher);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailIsNotUnique()
    {
        // Arrange
        _usersSearchable.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        CommandResult<Guid> result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.EmailNotUnique);
        _usersWritable.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_Should_PersistTheUserWithHashedPassword_WhenValid()
    {
        // Act
        CommandResult<Guid> result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _usersWritable.Received(1).Add(Arg.Is<User>(u =>
            u.Id == result.Data && u.Email.Value == "test@example.com" && u.PasswordHash == "hashed-password"));
        await UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Command_Should_RunInATransaction()
    {
        Command().ShouldBeAssignableTo<Application.Abstractions.Messaging.ITransactionalCommand>();
    }

    private static RegisterUserCommand Command() => new("Test@Example.com", "Test", "User", "Password123");
}
