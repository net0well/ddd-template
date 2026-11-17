using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Users;

internal sealed class UsersWritableRepository(ApplicationDbContext context) : IUsersWritableRepository, IInjectScoped
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
