namespace Application.Users;

internal static class RegisterPasswordRules
{
    public const int MinLength = 8;

    // Upper bound so a huge password cannot be used to make hashing expensive.
    public const int MaxLength = 128;
}
