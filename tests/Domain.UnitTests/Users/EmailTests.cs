using Domain.Users;

namespace Domain.UnitTests.Users;

public sealed class EmailTests
{
    [Fact]
    public void Create_Should_NormalizeToTrimmedLowerCase()
    {
        // Act
        var email = Email.Create("  John.Doe@Example.COM ");

        // Assert
        email.IsValid.ShouldBeTrue();
        email.Value.ShouldBe("john.doe@example.com");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("John <john@example.com>")]
    [InlineData("john@")]
    public void Create_Should_Notify_WhenTheAddressIsMalformed(string value)
    {
        // Act
        var email = Email.Create(value);

        // Assert
        email.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.EmailInvalid);
    }

    [Fact]
    public void Create_Should_Notify_WhenEmpty()
    {
        // Act
        var email = Email.Create(null);

        // Assert
        email.Notifications.ShouldHaveSingleItem().ShouldBe(UserErrors.EmailRequired);
    }

    [Fact]
    public void Equals_Should_CompareByValue()
    {
        // Act & Assert
        Email.Create("a@example.com").ShouldBe(Email.Create("A@EXAMPLE.com"));
        Email.Create("a@example.com").ShouldNotBe(Email.Create("b@example.com"));
    }
}
