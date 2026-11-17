using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;
using SharedKernel.Time;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IUsersSearchableRepository users,
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
        bool passwordMatches = passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null || !passwordMatches)
        {
            return CommandResult.Fail<AccessTokensResponse>(UserErrors.InvalidCredentials);
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
