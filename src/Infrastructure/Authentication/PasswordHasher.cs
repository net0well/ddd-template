using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Authentication;

// PBKDF2-HMAC-SHA512, format "{hash}-{salt}" in hex.
internal sealed class PasswordHasher : IPasswordHasher, IInjectSingleton
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 500000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    // Verified against when the account does not exist, so both paths cost the same.
    private static readonly Lazy<string> DummyHash = new(() => HashPassword(Guid.NewGuid().ToString()));

    public string Hash(string password) => HashPassword(password);

    public bool Verify(string password, string? passwordHash)
    {
        if (passwordHash is null)
        {
            Verify(password, DummyHash.Value);
            return false;
        }

        string[] parts = passwordHash.Split('-');
        byte[] hash = Convert.FromHexString(parts[0]);
        byte[] salt = Convert.FromHexString(parts[1]);

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }
}
