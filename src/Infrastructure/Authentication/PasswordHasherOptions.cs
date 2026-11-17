using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Authentication;

public sealed class PasswordHasherOptions
{
    public const string SectionName = "PasswordHasher";

    /// <summary>
    /// PBKDF2-HMAC-SHA512 iterations (OWASP: 210,000). Changing it does not invalidate stored passwords:
    /// each hash keeps its own count and is upgraded on the next successful login.
    /// </summary>
    [Range(100_000, 5_000_000)]
    public int Iterations { get; init; } = 210_000;
}
