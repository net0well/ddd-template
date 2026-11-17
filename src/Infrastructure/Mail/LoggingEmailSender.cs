using Application.Abstractions.Mail;
using Microsoft.Extensions.Logging;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Mail;

// Only logs. Replace with a real provider (SMTP, SendGrid, Postmark…) when you need to send mail.
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender, IInjectTransient
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email to {Recipient}: {Subject}", recipient, subject);

        return Task.CompletedTask;
    }
}
