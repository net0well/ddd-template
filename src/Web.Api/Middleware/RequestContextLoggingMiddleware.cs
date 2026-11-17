using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace Web.Api.Middleware;

/// <summary>
/// Adds a <c>CorrelationId</c> to every log of the request and echoes it in the response header.
/// A client-supplied id is accepted only if it is short and safe; otherwise the trace id is used, so logs
/// and traces line up.
/// </summary>
public sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";
    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = GetCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues values);

        string? supplied = values.FirstOrDefault();

        if (supplied is { Length: > 0 and <= MaxCorrelationIdLength } && supplied.All(IsSafe))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    // Prevents log forging (new lines, control characters) through the header.
    private static bool IsSafe(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.';
}
