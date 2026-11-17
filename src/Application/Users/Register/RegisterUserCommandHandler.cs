using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    UserRegistrationService registrationService,
    IUsersWritableRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<CommandResult<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        User user = await registrationService.RegisterAsync(
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password),
            cancellationToken);

        if (!user.IsValid)
        {
            return CommandResult.Fail<Guid>(user);
        }

        users.Add(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok(user.Id);
    }
}
