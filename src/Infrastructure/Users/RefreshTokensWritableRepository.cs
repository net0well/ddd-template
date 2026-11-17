using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Users;

internal sealed class RefreshTokensWritableRepository(ApplicationDbContext context) : IRefreshTokensWritableRepository, IInjectScoped
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedOnUtc == null && rt.ExpiresOnUtc > utcNow)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);

    public Task<int> DeleteExpiredAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Where(rt => rt.ExpiresOnUtc < expiredBeforeUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
