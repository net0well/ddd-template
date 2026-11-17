using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Authentication;

/// <summary>Bound from the <c>Jwt</c> section and validated at startup.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 key. At least 32 characters (256 bits). Keep it in user secrets / env vars.</summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpirationInMinutes { get; init; } = 60;

    [Range(1, 365)]
    public int RefreshTokenExpirationInDays { get; init; } = 7;

    /// <summary>
    /// A rotated token presented again within this window gets a 409 (no new tokens, session kept) instead of
    /// revoking every session: two tabs refreshing at the same time. 0 disables it.
    /// </summary>
    [Range(0, 300)]
    public int RefreshTokenReuseGracePeriodInSeconds { get; init; } = 30;
}
