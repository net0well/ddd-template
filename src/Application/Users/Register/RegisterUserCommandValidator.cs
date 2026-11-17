using Domain.Users;
using FluentValidation;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(User.NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(Email.MaxLength);
        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(RegisterPasswordRules.MinLength)
            .MaximumLength(RegisterPasswordRules.MaxLength);
    }
}
