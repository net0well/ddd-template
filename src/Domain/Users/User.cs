using SharedKernel.Notifications;
using SharedKernel.Primitives;

namespace Domain.Users;

public sealed class User : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 100;

    private readonly List<string> _roles = [];

    private User()
    {
    }

    public Email Email { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string PasswordHash { get; private set; }

    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    // Audit stamps are written by the persistence layer on save (see IAuditable).
    public DateTime CreatedOnUtc { get; }

    public Guid? CreatedBy { get; }

    public DateTime? ModifiedOnUtc { get; }

    public Guid? ModifiedBy { get; }

    /// <summary>
    /// Creates a <see cref="Role.Member"/>. Check <see cref="Notifiable.IsValid"/> before persisting.
    /// E-mail uniqueness is checked by <see cref="UserRegistrationService"/>.
    /// </summary>
    public static User Create(Email email, string? firstName, string? lastName, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Email = email,
            FirstName = firstName?.Trim() ?? string.Empty,
            LastName = lastName?.Trim() ?? string.Empty,
            PasswordHash = passwordHash
        };

        user._roles.Add(Role.Member);

        user.AddNotifications(email);
        user.CheckName(user.FirstName, UserErrors.FirstNameRequired, UserErrors.FirstNameTooLong);
        user.CheckName(user.LastName, UserErrors.LastNameRequired, UserErrors.LastNameTooLong);

        if (user.IsValid)
        {
            user.Raise(new UserRegisteredDomainEvent(user.Id, email.Value));
        }

        return user;
    }

    public void AssignRole(string role)
    {
        if (!Role.IsKnown(role))
        {
            AddNotification(UserErrors.UnknownRole(role));
            return;
        }

        if (!HasRole(role))
        {
            _roles.Add(role);
        }
    }

    public bool HasRole(string role) => _roles.Contains(role, StringComparer.Ordinal);

    /// <summary>Replaces the stored hash, e.g. when the hashing cost changed (rehash on login).</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }

    private void CheckName(string name, Notification required, Notification tooLong)
    {
        if (name.Length == 0)
        {
            AddNotification(required);
        }
        else if (name.Length > NameMaxLength)
        {
            AddNotification(tooLong);
        }
    }
}
