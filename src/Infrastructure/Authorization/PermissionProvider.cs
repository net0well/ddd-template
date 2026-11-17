using Application.Abstractions.Caching;
using Application.Users;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider(ApplicationDbContext context, HybridCache cache) : IInjectScoped
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string[] permissions = await cache.GetOrCreateWithTraceAsync(
            UserCacheKeys.Permissions(userId),
            async cancellation =>
            {
                IReadOnlyList<string>? roles = await context.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => user.Roles)
                    .SingleOrDefaultAsync(cancellation);

                return roles is null ? [] : RolePermissions.For(roles).ToArray();
            },
            CacheOptions,
            cancellationToken: cancellationToken);

        return [.. permissions];
    }
}
