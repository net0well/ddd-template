namespace Domain.Users;

/// <summary>
/// Permissions granted by each role. Roles are stored per user (data); this mapping is code because the
/// endpoints that require a permission are code too.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, string[]> PermissionsByRole = new(StringComparer.Ordinal)
    {
        [Role.Member] = [Permissions.UsersAccess],
        [Role.Admin] = [Permissions.UsersAccess, Permissions.UsersManage]
    };

    public static HashSet<string> For(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        return [.. roles.SelectMany(role =>
            PermissionsByRole.TryGetValue(role, out string[]? permissions) ? permissions : [])];
    }
}
