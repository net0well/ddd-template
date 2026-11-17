using System.Globalization;
using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA512 stored as <c>v1$iterations$salt$hash</c> (Base64), so the cost can change without
/// invalidating existing passwords. Hashes in the legacy <c>{hash}-{salt}</c> hex format are still accepted.
/// </summary>
internal sealed class PasswordHasher(IOptions<PasswordHasherOptions> options) : IPasswordHasher, IInjectSingleton
{
    private const string Version = "v1";
    private const char Separator = '$';
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int LegacyIterations = 500_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    private readonly int _iterations = options.Value.Iterations;

    // Verified against when the account does not exist, so both paths cost the same.
    private string? _dummyHash;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, HashSize);

        return string.Join(
            Separator,
            Version,
            _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string password, string? passwordHash)
    {
        if (passwordHash is null)
        {
            Verify(password, _dummyHash ??= Hash(Guid.NewGuid().ToString()));
            return PasswordVerificationResult.Failed;
        }

        if (!TryParse(passwordHash, out int iterations, out byte[] salt, out byte[] expected, out bool legacy))
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return PasswordVerificationResult.Failed;
        }

        return legacy || iterations != _iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash, out bool legacy)
    {
        iterations = 0;
        salt = hash = [];
        legacy = false;

        try
        {
            string[] parts = value.Split(Separator);

            if (parts is [Version, string count, string encodedSalt, string encodedHash] &&
                int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out iterations))
            {
                salt = Convert.FromBase64String(encodedSalt);
                hash = Convert.FromBase64String(encodedHash);
                return iterations > 0 && hash.Length > 0;
            }

            // Legacy format: "{hash}-{salt}" in hex, fixed cost.
            if (value.Split('-') is [string legacyHash, string legacySalt])
            {
                hash = Convert.FromHexString(legacyHash);
                salt = Convert.FromHexString(legacySalt);
                iterations = LegacyIterations;
                legacy = true;
                return hash.Length > 0;
            }
        }
        catch (FormatException)
        {
            // Corrupted value: treated as a failed verification.
        }

        return false;
    }
}
