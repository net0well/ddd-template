using Application.Abstractions.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Primitives;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Web.Api.Infrastructure;

/// <summary>
/// Opt-in deduplication for POST endpoints (<c>.WithIdempotency()</c>). The first request with a given
/// <c>Idempotency-Key</c> runs; a repeat while it is remembered gets 409. Requests without the header run normally.
/// </summary>
/// <remarks>
/// The key is scoped per user and endpoint, and released when the request fails, so the client can retry a
/// 4xx/5xx with the same key. It lives in HybridCache: add a distributed L2 cache (e.g. Redis) when running more
/// than one instance. Replaying the original response is not implemented.
/// </remarks>
internal sealed class IdempotencyFilter(HybridCache cache) : IEndpointFilter
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxKeyLength = 128;

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1)
    };

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out StringValues values) ||
            values.FirstOrDefault() is not { Length: > 0 } idempotencyKey)
        {
            return await next(context);
        }

        if (idempotencyKey.Length > MaxKeyLength)
        {
            return TypedResults.Json(
                CommandResult.Fail(Notification.Validation(IdempotencyKeyHeader, $"The key must have at most {MaxKeyLength} characters.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Resolved per request: the filter instance is shared by every request to the endpoint.
        IUserContext userContext = httpContext.RequestServices.GetRequiredService<IUserContext>();
        string owner = userContext.TryGetUserId(out Guid userId) ? userId.ToString() : "anonymous";
        string cacheKey = $"idempotency:{owner}:{httpContext.Request.Path}:{idempotencyKey}";

        bool isFirstRequest = false;

        // HybridCache runs the factory once even under concurrent calls, so only one request wins the key.
        await cache.GetOrCreateAsync(
            cacheKey,
            _ =>
            {
                isFirstRequest = true;
                return ValueTask.FromResult(true);
            },
            CacheOptions,
            cancellationToken: httpContext.RequestAborted);

        if (!isFirstRequest)
        {
            return TypedResults.Json(
                CommandResult.Fail(Notification.Conflict(
                    "Request.Duplicate",
                    "A request with this idempotency key has already been processed.")),
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            object? result = await next(context);

            if (result is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest })
            {
                await cache.RemoveAsync(cacheKey, CancellationToken.None);
            }

            return result;
        }
        catch
        {
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
            throw;
        }
    }
}
