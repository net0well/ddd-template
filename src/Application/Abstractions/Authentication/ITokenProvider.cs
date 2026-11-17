using Domain.Users;

namespace Application.Abstractions.Authentication;

public interface ITokenProvider
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>Signed access token (JWT).</summary>
    string Create(User user);

    /// <summary>Random refresh token for the client. Never stored as is.</summary>
    string GenerateRefreshToken();

    /// <summary>One-way hash of a refresh token — the value that is stored.</summary>
    string HashRefreshToken(string refreshToken);
}
