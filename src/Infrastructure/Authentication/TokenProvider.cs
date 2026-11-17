using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Authentication;
using Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Authentication;

internal sealed class TokenProvider(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : ITokenProvider, IInjectSingleton
{
    private const int RefreshTokenSizeInBytes = 64;

    private static readonly JsonWebTokenHandler TokenHandler = new();

    private readonly JwtOptions _options = options.Value;

    private readonly SigningCredentials _signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret)),
        SecurityAlgorithms.HmacSha256);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenExpirationInDays);

    public TimeSpan RefreshTokenReuseGracePeriod => TimeSpan.FromSeconds(_options.RefreshTokenReuseGracePeriodInSeconds);

    public string Create(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
            ]),
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = utcNow.AddMinutes(_options.ExpirationInMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = _signingCredentials
        };

        return TokenHandler.CreateToken(tokenDescriptor);
    }

    public string GenerateRefreshToken() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenSizeInBytes));

    // Unsalted SHA-256 is enough here: the token is 512 random bits, so there is nothing to brute-force.
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
