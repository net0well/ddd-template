using FluentValidation;

namespace Application.Users.SendWelcomeEmail;

internal sealed class SendWelcomeEmailCommandValidator : AbstractValidator<SendWelcomeEmailCommand>
{
    public SendWelcomeEmailCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
