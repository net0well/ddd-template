using SharedKernel.Persistence;

namespace Domain.Users;

/// <summary>
/// Read side of the <see cref="User"/> aggregate. Returns untracked instances.
/// </summary>
public interface IUsersSearchableRepository : ISearchableRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
