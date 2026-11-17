using SharedKernel.Notifications;

namespace Domain.Users;

/// <summary>
/// Notifications of the users feature. Keys follow <c>"Users.{Reason}"</c>.
/// </summary>
public static class UserErrors
{
    public static readonly Notification NotFoundByEmail = Notification.NotFound(
        "Users.NotFoundByEmail",
        "The user with the specified email was not found");

    public static readonly Notification Forbidden = Notification.Forbidden(
        "Users.Forbidden",
        "You are not allowed to perform this action.");

    public static readonly Notification EmailNotUnique = Notification.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    // One message for "no such user" and "wrong password": telling them apart lets anyone find out
    // which e-mails are registered.
    public static readonly Notification InvalidCredentials = Notification.Unauthorized(
        "Users.InvalidCredentials",
        "The email or password is incorrect.");

    public static readonly Notification InvalidRefreshToken = Notification.Unauthorized(
        "Users.InvalidRefreshToken",
        "The provided refresh token is invalid or has expired");

    public static readonly Notification EmailRequired = Notification.Validation(
        "Users.EmailRequired",
        "The email is required.");

    public static readonly Notification EmailTooLong = Notification.Validation(
        "Users.EmailTooLong",
        $"The email must have at most {Email.MaxLength} characters.");

    public static readonly Notification EmailInvalid = Notification.Validation(
        "Users.EmailInvalid",
        "The email is not a valid address.");

    public static readonly Notification FirstNameRequired = Notification.Validation(
        "Users.FirstNameRequired",
        "The first name is required.");

    public static readonly Notification FirstNameTooLong = Notification.Validation(
        "Users.FirstNameTooLong",
        $"The first name must have at most {User.NameMaxLength} characters.");

    public static readonly Notification LastNameRequired = Notification.Validation(
        "Users.LastNameRequired",
        "The last name is required.");

    public static readonly Notification LastNameTooLong = Notification.Validation(
        "Users.LastNameTooLong",
        $"The last name must have at most {User.NameMaxLength} characters.");

    public static Notification NotFound(Guid userId) => Notification.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Notification UnknownRole(string role) => Notification.BusinessRule(
        "Users.UnknownRole",
        $"The role '{role}' does not exist.");
}
