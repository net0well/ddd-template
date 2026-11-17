using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Users;

internal sealed class UsersSearchableRepository(ApplicationDbContext context) : IUsersSearchableRepository, IInjectScoped
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Id == userId, cancellationToken);

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == email, cancellationToken);
}
