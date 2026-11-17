namespace Domain.Users;

/// <summary>
/// Permission names. Endpoints require them with <c>.HasPermission(Permissions.X)</c>; roles grant them
/// through <see cref="RolePermissions"/>.
/// </summary>
public static class Permissions
{
    /// <summary>Read your own user profile.</summary>
    public const string UsersAccess = "users:access";

    /// <summary>Manage other users (example of an admin-only permission).</summary>
    public const string UsersManage = "users:manage";
}
