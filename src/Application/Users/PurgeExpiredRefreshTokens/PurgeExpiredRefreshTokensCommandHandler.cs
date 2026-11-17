using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Users.PurgeExpiredRefreshTokens;

// Revoked tokens that have not expired yet are kept: they are what detects the reuse of a stolen token.
internal sealed class PurgeExpiredRefreshTokensCommandHandler(
    IRefreshTokensWritableRepository refreshTokens,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PurgeExpiredRefreshTokensCommand, int>
{
    public async Task<CommandResult<int>> Handle(PurgeExpiredRefreshTokensCommand command, CancellationToken cancellationToken)
    {
        int deleted = await refreshTokens.DeleteExpiredAsync(dateTimeProvider.UtcNow, cancellationToken);

        return CommandResult.Ok(deleted);
    }
}
