using System.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

namespace Application.Abstractions.Caching;

public static class HybridCacheExtensions
{
    /// <summary>
    /// <see cref="HybridCache.GetOrCreateAsync{T}(string, Func{CancellationToken, ValueTask{T}}, HybridCacheEntryOptions?, IEnumerable{string}?, CancellationToken)"/>
    /// that keeps the caller's trace inside the factory.
    /// </summary>
    /// <remarks>
    /// HybridCache runs the factory outside the caller's execution context, so the queries of a cache miss would
    /// otherwise be orphan spans and their logs would have no <c>trace_id</c>.
    /// </remarks>
    public static ValueTask<T> GetOrCreateWithTraceAsync<T>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        return cache.GetOrCreateAsync(
            key,
            (Caller: Activity.Current, Factory: factory),
            static async (state, token) =>
            {
                Activity? previous = Activity.Current;
                Activity.Current = state.Caller;

                try
                {
                    return await state.Factory(token);
                }
                finally
                {
                    Activity.Current = previous;
                }
            },
            options,
            cancellationToken: cancellationToken);
    }
}
