using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.Extensions.Logging;
using SharedKernel.Results;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokensWritableRepository refreshTokens,
    IUsersSearchableRepository users,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCommandHandler> logger) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<CommandResult<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        RefreshToken? current = await refreshTokens.GetByTokenHashAsync(
            tokenProvider.HashRefreshToken(command.RefreshToken),
            cancellationToken);

        if (current is null)
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (current.WasRotatedWithin(tokenProvider.RefreshTokenReuseGracePeriod, utcNow))
        {
            // Rotated moments ago by another request of the same client: no new tokens, but the session stays.
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.RefreshTokenConflict);
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

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DataConflictException)
        {
            // Another request rotated the same token first (e.g. two browser tabs refreshing together).
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.RefreshTokenConflict);
        }

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
