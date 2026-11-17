namespace Application.Users;

public static class UserCacheKeys
{
    /// <summary>Permissions of a user. Invalidate it whenever the user's roles change.</summary>
    public static string Permissions(Guid userId) => $"users-{userId}-permissions";
}
