using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IUsersSearchableRepository users,
    IUsersWritableRepository writableUsers,
    IRefreshTokensWritableRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<CommandResult<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);

        User? user = email.IsValid ? await users.GetByEmailAsync(email, cancellationToken) : null;

        // Runs even when the user does not exist, so timing does not reveal registered e-mails.
        PasswordVerificationResult verification = passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null || verification == PasswordVerificationResult.Failed)
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Stored with an older format or cost: upgraded now that the plain password is known.
            User? tracked = await writableUsers.GetByIdAsync(user.Id, cancellationToken);
            tracked?.ChangePasswordHash(passwordHasher.Hash(command.Password));
        }

        string refreshToken = tokenProvider.GenerateRefreshToken();

        refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            tokenProvider.HashRefreshToken(refreshToken),
            dateTimeProvider.UtcNow,
            tokenProvider.RefreshTokenLifetime));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CommandResult.Ok(new AccessTokensResponse(tokenProvider.Create(user), refreshToken));
    }
}
