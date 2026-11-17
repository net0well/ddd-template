using Application.Abstractions.Messaging;

namespace Application.Users.Logout;

/// <summary>Ends every session of the current user by revoking all of their refresh tokens.</summary>
public sealed record LogoutUserCommand : ICommand;
