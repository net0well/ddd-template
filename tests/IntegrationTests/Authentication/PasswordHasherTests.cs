using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Authentication;

public sealed class PasswordHasherTests
{
    private const string Password = "Password123";

    private static readonly PasswordHasher Hasher = Create(100_000);

    [Fact]
    public void Hash_Should_StoreTheVersionAndTheCost_WithARandomSalt()
    {
        // Act
        string hash = Hasher.Hash(Password);

        // Assert
        string[] parts = hash.Split('$');
        parts.Length.ShouldBe(4);
        parts[0].ShouldBe("v1");
        parts[1].ShouldBe("100000");
        Hasher.Hash(Password).ShouldNotBe(hash);
    }

    [Fact]
    public void Verify_Should_Succeed_OnlyForTheRightPassword()
    {
        // Arrange
        string hash = Hasher.Hash(Password);

        // Act & Assert
        Hasher.Verify(Password, hash).ShouldBe(PasswordVerificationResult.Success);
        Hasher.Verify("wrong-password", hash).ShouldBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void Verify_Should_AskForARehash_WhenTheConfiguredCostChanged()
    {
        // Arrange
        string hash = Hasher.Hash(Password);

        // Act & Assert
        Create(120_000).Verify(Password, hash).ShouldBe(PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public void Verify_Should_AcceptTheLegacyFormat_AndAskForARehash()
    {
        // Arrange
        string legacy = LegacyHash(Password);

        // Act & Assert
        Hasher.Verify(Password, legacy).ShouldBe(PasswordVerificationResult.SuccessRehashNeeded);
        Hasher.Verify("wrong-password", legacy).ShouldBe(PasswordVerificationResult.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("zz-zz")]
    [InlineData("v1$abc$def$ghi")]
    [InlineData("v1$100000$!!!$!!!")]
    public void Verify_Should_Fail_ForMissingOrCorruptedHashes(string? hash) =>
        Hasher.Verify(Password, hash).ShouldBe(PasswordVerificationResult.Failed);

    /// <summary>The format used before versioning: <c>{hash}-{salt}</c> in hex, 500,000 iterations.</summary>
    internal static string LegacyHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 500_000, HashAlgorithmName.SHA512, 32);

        return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }

    private static PasswordHasher Create(int iterations) =>
        new(Options.Create(new PasswordHasherOptions { Iterations = iterations }));
}
