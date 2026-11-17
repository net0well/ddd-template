using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.Extensions.Logging;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokensWritableRepository refreshTokens,
    IUsersSearchableRepository users,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<RefreshTokenCommandHandler> logger) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<CommandResult<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;

        RefreshToken? current = await refreshTokens.GetByTokenHashAsync(
            tokenProvider.HashRefreshToken(command.RefreshToken),
            cancellationToken);

        if (current is null)
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (current.IsRevoked)
        {
            // A rotated token used again was stolen: end every session of the user.
            await RevokeAllSessionsAsync(current.UserId, utcNow, cancellationToken);

            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (current.IsExpired(utcNow))
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        User? user = await users.GetByIdAsync(current.UserId, cancellationToken);

        if (user is null)
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        string newRefreshToken = tokenProvider.GenerateRefreshToken();

        refreshTokens.Add(current.Rotate(
            tokenProvider.HashRefreshToken(newRefreshToken),
            utcNow,
            tokenProvider.RefreshTokenLifetime));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok(new AccessTokensResponse(tokenProvider.Create(user), newRefreshToken));
    }

    private async Task RevokeAllSessionsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", userId);

        foreach (RefreshToken token in await refreshTokens.GetActiveByUserIdAsync(userId, utcNow, cancellationToken))
        {
            token.Revoke(utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
