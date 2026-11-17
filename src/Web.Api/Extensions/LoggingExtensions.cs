using Application.Abstractions.Authentication;
using Infrastructure.Outbox;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Web.Api.Extensions;

internal static class LoggingExtensions
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {TraceId} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Serilog: levels and extra sinks from the <c>Serilog</c> config section; console as readable text in
    /// Development and as JSON elsewhere; OTLP export when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// </summary>
    // The outbox polls every second; its SQL would drown the command log (Information in Development).
    private static bool IsOutboxPolling(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("commandText", out LogEventPropertyValue? value) &&
        value is ScalarValue { Value: string sql } &&
        sql.StartsWith($"-- {OutboxDiagnostics.PollingQueryTag}", StringComparison.Ordinal);

    internal static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        string serviceName = builder.Environment.ApplicationName;
        bool isDevelopment = builder.Environment.IsDevelopment();
        bool exportToOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", serviceName)
                .Filter.ByExcluding(IsOutboxPolling);

            if (isDevelopment)
            {
                logger.WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: null);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            if (exportToOtlp)
            {
                // Endpoint, protocol and headers come from the standard OTEL_EXPORTER_OTLP_* variables.
                logger.WriteTo.OpenTelemetry(options =>
                    options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName });
            }
        });

        return builder;
    }

    /// <summary>One log line per request, with the user id; health checks and the jobs dashboard stay quiet.</summary>
    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app, string jobsDashboardPath) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                {
                    return LogEventLevel.Error;
                }

                PathString path = httpContext.Request.Path;

                bool isNoise = path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWithSegments(jobsDashboardPath, StringComparison.OrdinalIgnoreCase);

                return isNoise ? LogEventLevel.Verbose : LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("ClientIp", httpContext.GetClientIp());

                if (httpContext.RequestServices.GetRequiredService<IUserContext>().TryGetUserId(out Guid userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }
            };
        });
}
