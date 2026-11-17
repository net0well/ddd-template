using System.Globalization;
using System.Threading.RateLimiting;
using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel.Notifications;
using SharedKernel.Results;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimitingInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int globalPermitLimit = configuration.GetValue<int?>("RateLimiting:Global:PermitLimit") ?? 100;
        int globalWindowSeconds = configuration.GetValue<int?>("RateLimiting:Global:WindowInSeconds") ?? 60;
        int authPermitLimit = configuration.GetValue<int?>("RateLimiting:Authentication:PermitLimit") ?? 10;
        int authWindowSeconds = configuration.GetValue<int?>("RateLimiting:Authentication:WindowInSeconds") ?? 60;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            // Global fixed window, partitioned by authenticated user or client IP.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPermitLimit,
                        Window = TimeSpan.FromSeconds(globalWindowSeconds)
                    }));

            // Stricter policy for authentication endpoints, against brute force.
            options.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermitLimit,
                        Window = TimeSpan.FromSeconds(authWindowSeconds)
                    }));
        });

        return services;
    }

    private static string GetPartitionKey(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IUserContext>().TryGetUserId(out Guid userId)
            ? $"user:{userId}"
            : $"ip:{httpContext.GetClientIp() ?? "unknown"}";

    // Same envelope as every other error, plus Retry-After when the limiter knows it.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            CommandResult.Fail(Notification.BusinessRule("Request.RateLimited", "Too many requests. Try again later.")),
            cancellationToken);
    }
}
