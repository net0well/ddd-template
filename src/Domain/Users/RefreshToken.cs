using SharedKernel.Primitives;

namespace Domain.Users;

/// <summary>
/// Refresh token of a user. Only its hash is stored, and it is rotated on every use: a revoked token
/// presented again means it was stolen.
/// </summary>
public sealed class RefreshToken : AggregateRoot
{
    public const int TokenHashMaxLength = 128;

    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? RevokedOnUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedOnUtc.HasValue;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A refresh token must belong to a user.", nameof(userId));
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedOnUtc = utcNow,
            ExpiresOnUtc = utcNow.Add(lifetime)
        };
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresOnUtc;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    /// <summary>
    /// Revokes this token and returns its replacement. The caller must check <see cref="IsActive"/> first.
    /// </summary>
    public RefreshToken Rotate(string newTokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        if (!IsActive(utcNow))
        {
            throw new InvalidOperationException("Only an active refresh token can be rotated.");
        }

        RefreshToken replacement = Issue(UserId, newTokenHash, utcNow, lifetime);

        RevokedOnUtc = utcNow;
        ReplacedByTokenId = replacement.Id;

        return replacement;
    }

    public void Revoke(DateTime utcNow)
    {
        if (!IsRevoked)
        {
            RevokedOnUtc = utcNow;
        }
    }
}
