using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Web.Api.Extensions;

internal static class HealthCheckExtensions
{
    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependency checks). <c>/health</c>: ready to serve, checks
    /// the database. Details only in Development — they can expose infrastructure information.
    /// </summary>
    internal static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("health/live", new HealthCheckOptions { Predicate = _ => false })
            .DisableRateLimiting();

        var readinessOptions = new HealthCheckOptions();

        if (app.Environment.IsDevelopment())
        {
            readinessOptions.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        }

        app.MapHealthChecks("health", readinessOptions).DisableRateLimiting();

        return app;
    }
}
