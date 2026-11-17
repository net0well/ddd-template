using Domain.Users;

namespace Domain.UnitTests.Users;

public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Issue_Should_CreateAnActiveToken()
    {
        // Act
        var token = RefreshToken.Issue(UserId, "hash", UtcNow, Lifetime);

        // Assert
        token.IsActive(UtcNow).ShouldBeTrue();
        token.ExpiresOnUtc.ShouldBe(UtcNow + Lifetime);
    }

    [Fact]
    public void IsActive_Should_BeFalse_OnceExpired()
    {
        // Arrange
        var token = RefreshToken.Issue(UserId, "hash", UtcNow, Lifetime);

        // Act & Assert
        token.IsActive(UtcNow + Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_Should_RevokeTheTokenAndLinkItToTheReplacement()
    {
        // Arrange
        var token = RefreshToken.Issue(UserId, "old-hash", UtcNow, Lifetime);

        // Act
        RefreshToken replacement = token.Rotate("new-hash", UtcNow.AddHours(1), Lifetime);

        // Assert
        token.IsRevoked.ShouldBeTrue();
        token.ReplacedByTokenId.ShouldBe(replacement.Id);
        replacement.UserId.ShouldBe(UserId);
        replacement.TokenHash.ShouldBe("new-hash");
        replacement.IsActive(UtcNow.AddHours(1)).ShouldBeTrue();
    }

    [Fact]
    public void Rotate_Should_Throw_WhenTheTokenIsNotActive()
    {
        // Arrange
        var token = RefreshToken.Issue(UserId, "hash", UtcNow, Lifetime);
        token.Revoke(UtcNow);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => token.Rotate("new-hash", UtcNow, Lifetime));
    }

    [Fact]
    public void Revoke_Should_KeepTheFirstRevocationDate()
    {
        // Arrange
        var token = RefreshToken.Issue(UserId, "hash", UtcNow, Lifetime);

        // Act
        token.Revoke(UtcNow);
        token.Revoke(UtcNow.AddHours(1));

        // Assert
        token.RevokedOnUtc.ShouldBe(UtcNow);
    }
}
