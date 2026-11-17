using SharedKernel.DependencyInjection;
using SharedKernel.Notifications;

namespace Domain.Users;

/// <summary>
/// Domain service for registration: e-mail uniqueness is a business rule that <see cref="User"/> cannot
/// enforce alone, because it depends on the other users.
/// </summary>
/// <remarks>
/// A domain service decides; it does not save or send anything. The handler persists what it returns.
/// </remarks>
public sealed class UserRegistrationService(IUsersSearchableRepository users) : IInjectScoped
{
    /// <summary>
    /// Builds the user and applies the registration rules. Persist it only when <see cref="Notifiable.IsValid"/>.
    /// </summary>
    public async Task<User> RegisterAsync(
        string? email,
        string? firstName,
        string? lastName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        var user = User.Create(Email.Create(email), firstName, lastName, passwordHash);

        if (user.IsValid && await users.ExistsByEmailAsync(user.Email, cancellationToken))
        {
            user.AddNotification(UserErrors.EmailNotUnique);
        }

        return user;
    }
}
