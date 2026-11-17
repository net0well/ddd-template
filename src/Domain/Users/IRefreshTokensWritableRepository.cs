using SharedKernel.Persistence;

namespace Domain.Users;

/// <summary>
/// Write side of <see cref="RefreshToken"/>. No searchable side: tokens are only read in order to change them.
/// </summary>
public interface IRefreshTokensWritableRepository : IWritableRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tokens of the user that are neither revoked nor expired.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);

    /// <summary>
    /// Bulk delete that runs <b>immediately</b> (it does not wait for the unit of work).
    /// </summary>
    /// <returns>How many tokens were deleted.</returns>
    Task<int> DeleteExpiredAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken = default);
}
