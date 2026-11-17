using SharedKernel.Persistence;

namespace Domain.Users;

/// <summary>
/// Write side of the <see cref="User"/> aggregate. Changes are committed by the unit of work.
/// </summary>
public interface IUsersWritableRepository : IWritableRepository
{
    /// <summary>Loads the user tracked for change.</summary>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(User user);
}
