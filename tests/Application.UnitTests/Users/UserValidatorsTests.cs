using Application.Users;
using Application.Users.Login;
using Application.Users.Register;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Users;

public sealed class UserValidatorsTests
{
    private readonly LoginUserCommandValidator _loginValidator = new();
    private readonly RegisterUserCommandValidator _registerValidator = new();

    [Theory]
    [InlineData("", "Password123")]
    [InlineData("not-an-email", "Password123")]
    [InlineData("test@example.com", "")]
    public void LoginValidator_Should_RejectMissingOrMalformedCredentials(string email, string password)
    {
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(new LoginUserCommand(email, password));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void LoginValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        TestValidationResult<LoginUserCommand> result =
            _loginValidator.TestValidate(new LoginUserCommand("test@example.com", "Password123"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsTooShort()
    {
        TestValidationResult<RegisterUserCommand> result =
            _registerValidator.TestValidate(new RegisterUserCommand("test@example.com", "Test", "User", "short"));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsTooLong()
    {
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(
            new RegisterUserCommand("test@example.com", "Test", "User", new string('a', RegisterPasswordRules.MaxLength + 1)));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }
}
