using Application.Abstractions.Mail;
using Application.Abstractions.Messaging;
using SharedKernel.Results;

namespace Application.Users.SendWelcomeEmail;

internal sealed class SendWelcomeEmailCommandHandler(IEmailSender emailSender)
    : ICommandHandler<SendWelcomeEmailCommand>
{
    public async Task<CommandResult> Handle(SendWelcomeEmailCommand command, CancellationToken cancellationToken)
    {
        await emailSender.SendAsync(
            command.Email,
            "Welcome!",
            "Thanks for registering. We're glad to have you on board.",
            cancellationToken);

        return CommandResult.Ok();
    }
}
