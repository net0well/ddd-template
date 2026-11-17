using Application.Abstractions.Messaging;

namespace Application.Users.Register;

// Transactional as an example: registration is where related aggregates (profile, settings…) usually get
// created together with the user.
public sealed record RegisterUserCommand(string Email, string FirstName, string LastName, string Password)
    : ICommand<Guid>, ITransactionalCommand;
