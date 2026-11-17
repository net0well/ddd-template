using Application.Abstractions.Messaging;

namespace Application.Users.SendWelcomeEmail;

public sealed record SendWelcomeEmailCommand(Guid UserId, string Email) : ICommand;
