using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Web.Api.Extensions;

/// <summary>
/// Turns a <see cref="CommandResult"/> into the HTTP response: the result itself is the body
/// (<c>{ success, data, notifications }</c>) and the notifications decide the status code.
/// </summary>
public static class CommandResultExtensions
{
    // When notifications of different types are mixed, the first match in this list wins.
    private static readonly (NotificationType Type, int StatusCode)[] StatusCodePrecedence =
    [
        (NotificationType.Failure, StatusCodes.Status500InternalServerError),
        (NotificationType.Unauthorized, StatusCodes.Status401Unauthorized),
        (NotificationType.Forbidden, StatusCodes.Status403Forbidden),
        (NotificationType.NotFound, StatusCodes.Status404NotFound),
        (NotificationType.Conflict, StatusCodes.Status409Conflict),
        (NotificationType.BusinessRule, StatusCodes.Status400BadRequest),
        (NotificationType.Validation, StatusCodes.Status400BadRequest)
    ];

    public static IResult ToHttpResult(this CommandResult result, int successStatusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Typed as object so a CommandResult<T> passed here still serializes its Data.
        return TypedResults.Json<object>(result, statusCode: GetStatusCode(result, successStatusCode));
    }

    public static IResult ToHttpResult<TData>(this CommandResult<TData> result, int successStatusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(result);

        return TypedResults.Json(result, statusCode: GetStatusCode(result, successStatusCode));
    }

    public static int ToStatusCode(this IReadOnlyCollection<Notification> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        foreach ((NotificationType type, int statusCode) in StatusCodePrecedence)
        {
            if (notifications.Any(notification => notification.Type == type))
            {
                return statusCode;
            }
        }

        return StatusCodes.Status400BadRequest;
    }

    private static int GetStatusCode(CommandResult result, int successStatusCode) =>
        result.Success ? successStatusCode : result.Notifications.ToStatusCode();
}
