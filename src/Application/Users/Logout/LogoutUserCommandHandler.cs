using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Users.Logout;

// Access tokens already issued stay valid until they expire (short-lived by design).
internal sealed class LogoutUserCommandHandler(
    IRefreshTokensWritableRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<LogoutUserCommand>
{
    public async Task<CommandResult> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;

        foreach (RefreshToken token in await refreshTokens.GetActiveByUserIdAsync(userContext.UserId, utcNow, cancellationToken))
        {
            token.Revoke(utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok();
    }
}
