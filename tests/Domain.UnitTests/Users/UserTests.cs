using Domain.Users;

namespace Domain.UnitTests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_Should_BuildAMemberAndRaiseRegisteredEvent()
    {
        // Act
        var user = User.Create(Email.Create("john@example.com"), "John", "Doe", "hash");

        // Assert
        user.IsValid.ShouldBeTrue();
        user.Roles.ShouldBe([Role.Member]);
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserRegisteredDomainEvent(user.Id, "john@example.com"));
    }

    [Fact]
    public void Create_Should_MergeTheEmailNotifications_AndCheckTheNames()
    {
        // Act
        var user = User.Create(Email.Create("invalid"), " ", new string('x', User.NameMaxLength + 1), "hash");

        // Assert
        user.Notifications.ShouldBe(
            [UserErrors.EmailInvalid, UserErrors.FirstNameRequired, UserErrors.LastNameTooLong],
            ignoreOrder: true);
        user.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AssignRole_Should_AddAKnownRoleOnce()
    {
        // Arrange
        var user = User.Create(Email.Create("john@example.com"), "John", "Doe", "hash");

        // Act
        user.AssignRole(Role.Admin);
        user.AssignRole(Role.Admin);

        // Assert
        user.Roles.ShouldBe([Role.Member, Role.Admin]);
        RolePermissions.For(user.Roles).ShouldContain(Permissions.UsersManage);
    }

    [Fact]
    public void AssignRole_Should_Notify_WhenTheRoleDoesNotExist()
    {
        // Arrange
        var user = User.Create(Email.Create("john@example.com"), "John", "Doe", "hash");

        // Act
        user.AssignRole("SuperUser");

        // Assert
        user.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.UnknownRole("SuperUser"));
        user.Roles.ShouldBe([Role.Member]);
    }
}
