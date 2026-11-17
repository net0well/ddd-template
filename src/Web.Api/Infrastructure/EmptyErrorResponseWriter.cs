using Microsoft.AspNetCore.Diagnostics;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Web.Api.Infrastructure;

/// <summary>
/// Gives the standard envelope to error responses the framework sends without a body (401 from the JWT
/// handler, 403 from a policy, 404 for an unknown route, 405…), so every error has the same shape.
/// </summary>
internal static class EmptyErrorResponseWriter
{
    public static async Task WriteAsync(StatusCodeContext context)
    {
        HttpResponse response = context.HttpContext.Response;

        await response.WriteAsJsonAsync(
            CommandResult.Fail(ToNotification(response.StatusCode)),
            context.HttpContext.RequestAborted);
    }

    private static Notification ToNotification(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => Notification.Unauthorized(
            "Request.Unauthorized",
            "Authentication is required, or the access token is invalid or expired."),
        StatusCodes.Status403Forbidden => Notification.Forbidden(
            "Request.Forbidden",
            "You do not have permission to perform this action."),
        StatusCodes.Status404NotFound => Notification.NotFound(
            "Request.NotFound",
            "The requested resource does not exist."),
        StatusCodes.Status405MethodNotAllowed => Notification.Validation(
            "Request.MethodNotAllowed",
            "The HTTP method is not allowed for this resource."),
        >= StatusCodes.Status500InternalServerError => Notification.Failure(
            "Server.Failure",
            "An unexpected error occurred."),
        _ => Notification.Validation(
            "Request.Invalid",
            "The request could not be processed.")
    };
}
