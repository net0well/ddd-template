using Application.Abstractions.Data;
using Microsoft.AspNetCore.Diagnostics;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Web.Api.Infrastructure;

/// <summary>
/// Last line of defense: turns unhandled exceptions into the standard response envelope.
/// Expected failures never get here — they are notifications.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int statusCode, Notification notification) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning(exception, "Request failed with status {StatusCode}", statusCode);
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(CommandResult.Fail(notification), cancellationToken);

        return true;
    }

    private static (int StatusCode, Notification Notification) Map(Exception exception) => exception switch
    {
        // Malformed JSON, missing body, wrong route/query value types.
        BadHttpRequestException badRequest => (
            badRequest.StatusCode,
            Notification.Validation("Request.Invalid", "The request is malformed or has invalid values.")),

        DataConflictException conflict => (
            StatusCodes.Status409Conflict,
            Notification.Conflict("Request.Conflict", conflict.Message)),

        _ => (
            StatusCodes.Status500InternalServerError,
            Notification.Failure("Server.Failure", "An unexpected error occurred."))
    };
}
