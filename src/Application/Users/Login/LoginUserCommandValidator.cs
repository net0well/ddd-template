using Domain.Users;
using FluentValidation;

namespace Application.Users.Login;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(Email.MaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(RegisterPasswordRules.MaxLength);
    }
}
