using Domain.Users;

namespace Domain.UnitTests.Users;

public sealed class UserRegistrationServiceTests
{
    private readonly IUsersSearchableRepository _users = Substitute.For<IUsersSearchableRepository>();

    [Fact]
    public async Task RegisterAsync_Should_NotifyConflict_WhenTheEmailIsInUse()
    {
        // Arrange
        _users.ExistsByEmailAsync(Email.Create("john@example.com"), Arg.Any<CancellationToken>()).Returns(true);
        var service = new UserRegistrationService(_users);

        // Act
        User user = await service.RegisterAsync("John@Example.com", "John", "Doe", "hash");

        // Assert
        user.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.EmailNotUnique);
    }

    [Fact]
    public async Task RegisterAsync_Should_NotQueryTheDatabase_WhenTheUserIsAlreadyInvalid()
    {
        // Arrange
        var service = new UserRegistrationService(_users);

        // Act
        User user = await service.RegisterAsync("invalid", "John", "Doe", "hash");

        // Assert
        user.IsValid.ShouldBeFalse();
        await _users.DidNotReceiveWithAnyArgs().ExistsByEmailAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_Should_ReturnAValidUser_WhenEveryRulePasses()
    {
        // Arrange
        var service = new UserRegistrationService(_users);

        // Act
        User user = await service.RegisterAsync("john@example.com", "John", "Doe", "hash");

        // Assert
        user.IsValid.ShouldBeTrue();
        user.Email.Value.ShouldBe("john@example.com");
    }
}
