namespace Domain.Users;

/// <summary>
/// The roles a user can have. A role is a named set of permissions (see <see cref="RolePermissions"/>).
/// </summary>
public static class Role
{
    /// <summary>Every registered user.</summary>
    public const string Member = "Member";

    /// <summary>Back-office access. Assign it explicitly (there is no endpoint for it on purpose).</summary>
    public const string Admin = "Admin";

    public static IReadOnlyCollection<string> All { get; } = [Member, Admin];

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.Ordinal);
}
